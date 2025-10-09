// ClipboardBridge.cpp
#include <windows.h>
#include <stdint.h>
#include <cstring>
#include <wincodec.h>   // WIC
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "windowscodecs.lib")

#ifndef BI_JPEG
#define BI_JPEG  4
#endif
#ifndef BI_PNG
#define BI_PNG   5
#endif

struct ComInit {
    HRESULT hr;
    ComInit() : hr(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED)) {}
    ~ComInit() { if (SUCCEEDED(hr)) CoUninitialize(); }
};

// 用 WIC 把内存中的 PNG/JPEG 解成 32bpp BGRA
static int decode_via_wic(const BYTE* buf, size_t len,
    unsigned char** outData, int* outW, int* outH, int* outStride)
{
    if (!buf || len == 0) return 0;
    *outData = nullptr; *outW = *outH = *outStride = 0;

    ComInit com;
    IWICImagingFactory* factory = nullptr;
    HRESULT hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&factory));
    if (FAILED(hr)) return 0;

    IWICStream* stream = nullptr;
    hr = factory->CreateStream(&stream);
    if (SUCCEEDED(hr)) hr = stream->InitializeFromMemory((BYTE*)buf, (DWORD)len);

    IWICBitmapDecoder* decoder = nullptr;
    if (SUCCEEDED(hr)) hr = factory->CreateDecoderFromStream(stream, nullptr, WICDecodeMetadataCacheOnLoad, &decoder);

    IWICBitmapFrameDecode* frame = nullptr;
    if (SUCCEEDED(hr)) hr = decoder->GetFrame(0, &frame);

    IWICFormatConverter* conv = nullptr;
    if (SUCCEEDED(hr)) hr = factory->CreateFormatConverter(&conv);
    if (SUCCEEDED(hr)) hr = conv->Initialize(frame, GUID_WICPixelFormat32bppBGRA,
        WICBitmapDitherTypeNone, nullptr, 0.f,
        WICBitmapPaletteTypeCustom);

    UINT w = 0, h = 0;
    if (SUCCEEDED(hr)) hr = conv->GetSize(&w, &h);

    if (FAILED(hr) || w == 0 || h == 0) {
        if (conv) conv->Release();
        if (frame) frame->Release();
        if (decoder) decoder->Release();
        if (stream) stream->Release();
        if (factory) factory->Release();
        return 0;
    }

    int stride = (int)w * 4;
    size_t total = (size_t)stride * (size_t)h;
    BYTE* dst = (BYTE*)CoTaskMemAlloc(total);
    if (!dst) {
        if (conv) conv->Release(); if (frame) frame->Release();
        if (decoder) decoder->Release(); if (stream) stream->Release();
        if (factory) factory->Release();
        return 0;
    }

    hr = conv->CopyPixels(nullptr, stride, (UINT)total, dst);
    if (FAILED(hr)) {
        CoTaskMemFree(dst);
        if (conv) conv->Release(); if (frame) frame->Release();
        if (decoder) decoder->Release(); if (stream) stream->Release();
        if (factory) factory->Release();
        return 0;
    }

    *outData = dst; *outW = (int)w; *outH = (int)h; *outStride = stride;

    if (conv) conv->Release();
    if (frame) frame->Release();
    if (decoder) decoder->Release();
    if (stream) stream->Release();
    if (factory) factory->Release();
    return 1;
}

// 动态解析掩码：把任意掩码位宽缩放到 8bit
static inline BYTE expand_from_mask(uint32_t v, uint32_t mask)
{
    if (!mask) return 0;
    // 计算右移位数（低位连续 0 的个数）
    int shift = 0;
    uint32_t m = mask;
    while ((m & 1u) == 0u) { m >>= 1; ++shift; }
    // 计算有效位宽（连续的 1）
    int bits = 0;
    while (m & 1u) { m >>= 1; ++bits; }
    if (bits <= 0) return 0;

    uint32_t raw = (v & mask) >> shift;                  // 原始分量值
    if (bits == 8) return (BYTE)raw;                     // 正好 8bit，无需缩放
    uint32_t maxv = (1u << bits) - 1u;
    // 四舍五入缩放到 0..255
    return (BYTE)((raw * 255u + (maxv / 2)) / maxv);
}

extern "C" {

    // 返回 1 成功，0 失败；outData 用 CoTaskMemAlloc 分配；C# 用 FreeBuffer 释放
    __declspec(dllexport) int __stdcall GetClipboardImageBGRA32(
        unsigned char** outData, int* outW, int* outH, int* outStride)
    {
        if (!outData || !outW || !outH || !outStride) return 0;
        *outData = nullptr; *outW = *outH = *outStride = 0;

        auto extract_from_dib = [&](const BYTE* dib, size_t len)->int {
            if (!dib || len < 40) return 0;

            auto RD32 = [&](int off)->uint32_t { return *(const uint32_t*)(dib + off); };
            auto RD16 = [&](int off)->uint16_t { return *(const uint16_t*)(dib + off); };

            int biSize = (int)RD32(0);   // 40/108/124
            int width = (int)RD32(4);
            int height = (int)RD32(8);   // +:bottom-up, -:top-down
            int planes = (int)RD16(12);
            int bpp = (int)RD16(14);  // 24/32
            int compress = (int)RD32(16);  // 0=BI_RGB, 3=BI_BITFIELDS, 4=JPEG, 5=PNG
            int sizeImage = (int)RD32(20);  // 可能为0
            int clrUsed = (len >= 36) ? (int)RD32(32) : 0;

            if (biSize != 40 && biSize != 108 && biSize != 124) biSize = 40;

            int paletteEntries = 0;
            if (bpp <= 8) paletteEntries = (clrUsed > 0) ? clrUsed : (1 << bpp);
            int paletteSize = paletteEntries * 4;

            // 默认 BGRA 掩码
            uint32_t maskR = 0x00FF0000, maskG = 0x0000FF00, maskB = 0x000000FF, maskA = 0xFF000000;

            // V4/V5: 掩码在头里
            if (biSize >= 108) {
                if (len >= 54) maskR = RD32(40);
                if (len >= 58) maskG = RD32(44);
                if (len >= 62) maskB = RD32(48);
                if (len >= 66) maskA = RD32(52); // 可能为 0
            }
            // V3 + BITFIELDS: 头后 3*DWORD
            int masksSize = 0;
            if (biSize == 40 && ((compress == 3) && (bpp == 16 || bpp == 32))) {
                masksSize = 12;
                if ((int)len >= 52) {
                    maskR = RD32(40); maskG = RD32(44); maskB = RD32(48);
                    // A 可能未提供
                }
            }
            if (maskA == 0) maskA = 0xFF000000; // 若不给 A 掩码，默认不透明

            // 计算像素偏移：头 + 掩码 + 调色板
            int offBits = biSize + masksSize + paletteSize;

            // **关键修复**：V5 可能带 ICC Profile，把 offBits 提升到 Profile 之后
            if (biSize >= 124) {
                uint32_t v5ProfileData = RD32(112); // 相对头起始
                uint32_t v5ProfileSize = RD32(116);
                uint64_t profEnd = (uint64_t)v5ProfileData + (uint64_t)v5ProfileSize;
                if (v5ProfileSize > 0 && v5ProfileData >= (uint32_t)biSize && profEnd <= (uint64_t)len) {
                    if ((int)profEnd > offBits) offBits = (int)profEnd;
                }
            }
            if ((int)len < offBits) return 0;

            // BI_PNG / BI_JPEG：像素是压缩流，交给 WIC
            if (compress == BI_PNG || compress == BI_JPEG) {
                size_t avail = (size_t)len - (size_t)offBits;
                size_t blobSize = (sizeImage > 0 && (size_t)offBits + (size_t)sizeImage <= len)
                    ? (size_t)sizeImage : avail;
                return decode_via_wic(dib + offBits, blobSize, outData, outW, outH, outStride);
            }

            int absH = (height < 0) ? -height : height;
            if (width <= 0 || absH <= 0) return 0;

            int strideSrc = ((width * bpp + 31) / 32) * 4;
            uint64_t needed = (sizeImage > 0) ? (uint64_t)offBits + (uint64_t)sizeImage
                : (uint64_t)offBits + (uint64_t)strideSrc * (uint64_t)absH;
            if (needed > (uint64_t)len) return 0;

            int strideDst = width * 4;
            size_t total = (size_t)strideDst * (size_t)absH;
            BYTE* dst = (BYTE*)CoTaskMemAlloc(total);
            if (!dst) return 0;
            std::memset(dst, 0, total);

            if (bpp == 32) {
                bool masksAreBGRA = (maskR == 0x00FF0000 && maskG == 0x0000FF00 && maskB == 0x000000FF && (maskA == 0xFF000000 || maskA == 0));
                bool anyColorNonZero = false;
                bool anyAlphaNonZero = false;

                for (int row = 0; row < absH; ++row) {
                    int srcRow = (height > 0) ? (absH - 1 - row) : row; // bottom-up→top-down
                    const BYTE* src = dib + offBits + (size_t)srcRow * (size_t)strideSrc;
                    BYTE* out = dst + (size_t)row * (size_t)strideDst;

                    if (masksAreBGRA) {
                        size_t toCopy = (size_t)strideDst <= (size_t)strideSrc ? (size_t)strideDst : (size_t)strideSrc;
                        std::memcpy(out, src, toCopy);

                        // 统计 alpha/颜色是否有值
                        for (int x = 0; x < width; ++x) {
                            BYTE b = out[x * 4 + 0], g = out[x * 4 + 1], r = out[x * 4 + 2], a = out[x * 4 + 3];
                            if (r | g | b) anyColorNonZero = true;
                            if (a) anyAlphaNonZero = true;
                        }
                    }
                    else {
                        for (int x = 0; x < width; ++x) {
                            uint32_t v = *(const uint32_t*)(src + (size_t)x * 4);
                            BYTE r = expand_from_mask(v, maskR);
                            BYTE g = expand_from_mask(v, maskG);
                            BYTE b = expand_from_mask(v, maskB);
                            BYTE a = maskA ? expand_from_mask(v, maskA) : 0xFF;
                            BYTE* d = out + (size_t)x * 4;
                            d[0] = b; d[1] = g; d[2] = r; d[3] = a;

                            if (r | g | b) anyColorNonZero = true;
                            if (a) anyAlphaNonZero = true;
                        }
                    }
                }

                // 兜底：颜色有值而 alpha 全 0（部分工具写了未预乘/或错误的 A 掩码）
                if (!anyAlphaNonZero && anyColorNonZero) {
                    for (size_t i = 3; i < total; i += 4) dst[i] = 0xFF;
                }

                *outData = dst; *outW = width; *outH = absH; *outStride = strideDst;
                return 1;
            }
            else if (bpp == 24) {
                for (int row = 0; row < absH; ++row) {
                    int srcRow = (height > 0) ? (absH - 1 - row) : row;
                    const BYTE* src = dib + offBits + (size_t)srcRow * (size_t)strideSrc;
                    BYTE* out = dst + (size_t)row * (size_t)strideDst;
                    for (int x = 0; x < width; ++x) {
                        const BYTE* s = src + (size_t)x * 3;
                        BYTE* d = out + (size_t)x * 4;
                        d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; d[3] = 0xFF;
                    }
                }
                *outData = dst; *outW = width; *outH = absH; *outStride = strideDst;
                return 1;
            }
            return 0; // 其他位深暂不支持
            };

        auto extract_from_hbitmap = [&](HBITMAP hbm)->int {
            if (!hbm) return 0;
            BITMAP bm{};
            if (!GetObject(hbm, sizeof(bm), &bm)) return 0;

            int width = bm.bmWidth;
            int height = bm.bmHeight;
            int absH = (height < 0) ? -height : height;

            BITMAPINFO bi{};
            bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
            bi.bmiHeader.biWidth = width;
            bi.bmiHeader.biHeight = -absH;  // top-down
            bi.bmiHeader.biPlanes = 1;
            bi.bmiHeader.biBitCount = 32;   // 直接取 32bpp
            bi.bmiHeader.biCompression = BI_RGB;

            int stride = width * 4;
            size_t total = (size_t)stride * (size_t)absH;
            BYTE* dst = (BYTE*)CoTaskMemAlloc(total);
            if (!dst) return 0;

            HDC hdc = GetDC(NULL);
            int got = GetDIBits(hdc, hbm, 0, absH, dst, &bi, DIB_RGB_COLORS);
            ReleaseDC(NULL, hdc);
            if (got == 0) { CoTaskMemFree(dst); return 0; }

            *outData = dst; *outW = width; *outH = absH; *outStride = stride;
            return 1;
            };

        if (!OpenClipboard(NULL)) return 0;
        int ok = 0;
        do {
            // 1) CF_DIB (优先，更标准兼容)
            if (HANDLE h = GetClipboardData(CF_DIB)) {
                SIZE_T sz = (SIZE_T)GlobalSize(h);
                const BYTE* p = (const BYTE*)GlobalLock(h);
                if (p && sz) ok = extract_from_dib(p, sz);
                if (p) GlobalUnlock(h);
                if (ok) break;
            }
            // 2) CF_DIBV5 (次选，部分软件V5头有特殊数据)
            if (HANDLE h = GetClipboardData(17 /*CF_DIBV5*/)) {
                SIZE_T sz = (SIZE_T)GlobalSize(h);
                const BYTE* p = (const BYTE*)GlobalLock(h);
                if (p && sz) ok = extract_from_dib(p, sz);
                if (p) GlobalUnlock(h);
                if (ok) break;
            }
            // 3) CF_BITMAP
            if (HANDLE h = GetClipboardData(CF_BITMAP)) {
                ok = extract_from_hbitmap((HBITMAP)h);
                if (ok) break;
            }
        } while (0);
        CloseClipboard();

        return ok;
    }

    __declspec(dllexport) void __stdcall FreeBuffer(unsigned char* p)
    {
        if (p) CoTaskMemFree(p);
    }

} // extern "C"
