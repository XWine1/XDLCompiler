#pragma once
#include <objbase.h>
#include <cstdint>
#include <compare>
#include <wil/result_macros.h>

#ifdef _CPPRTTI
#include <rttidata.h>
#include <typeinfo>
#endif

#ifndef CINTERFACE
struct IUnknownVtbl
{
    HRESULT(*QueryInterface)(IUnknown *, REFIID riid, void **ppvObject);
    ULONG(*AddRef)(IUnknown *);
    ULONG(*Release)(IUnknown *);
};
#endif

struct abi_t
{
    uint32_t Major = 0;
    uint32_t Minor = 0;
    uint32_t Build = 0;
    uint32_t Revision = 0;
    auto operator<=>(abi_t const &other) const noexcept = default;
    bool operator==(abi_t const &other) const noexcept = default;
};

// {7E93844E-159A-4D07-9910-87E9D65ECE00}
static const GUID GUID_UnwrapInterface = { 0x7E93844E, 0x159A, 0x4D07, { 0x99, 0x10, 0x87, 0xE9, 0xD6, 0x5E, 0xCE, 0x00 } };

template<typename T>
using reg_return_t = std::conditional_t<(std::is_class_v<T> || std::is_union_v<T>) && (sizeof(T) <= sizeof(std::uintptr_t)), std::uintptr_t, T>;

template<typename T>
FORCEINLINE reg_return_t<T> to_reg_return(T value)
{
    if constexpr (std::is_same_v<T, reg_return_t<T>>)
        return value;

    reg_return_t<T> reg = {};
    memcpy(&reg, &value, sizeof(T));
    return reg;
}

namespace xcom
{
    namespace impl
    {
        template<typename T>
        inline constexpr GUID guid_v = __uuidof(T);
    }

    template<typename T>
    inline constexpr GUID guid_of() { return impl::guid_v<T>; }

    template<template<abi_t> typename T>
    inline constexpr GUID guid_of() { return guid_of<T<abi_t{}>>(); }

    inline void StubHandler(char const *name, void *object)
    {
    #ifdef _CPPRTTI
        char const *type = object ? ((type_info const *)__RTtypeid(object))->name() : "STUB";
    #else
        char const *type = "STUB";
    #endif
        MessageBoxA(nullptr, name, type, MB_ICONERROR);
    #ifdef _DEBUG
        DebugBreak();
    #endif
        ExitProcess(0);
    }

    inline void TodoHandler(char const *name, void *object)
    {
    #ifdef _CPPRTTI
        char const *type = object ? ((type_info const *)__RTtypeid(object))->name() : nullptr;
    #else
        char const *type = nullptr;
    #endif
        OutputDebugStringA("TODO: ");
        OutputDebugStringA(name);

        if (type != nullptr)
        {
            OutputDebugStringA("(from ");
            OutputDebugStringA(type);
            OutputDebugStringA(")");
        }

        OutputDebugStringA("\n");
    }

    inline HRESULT UnwrapInterface(_In_opt_ IUnknown *pUnknown, _Outptr_ void **ppvObject)
    {
        HRESULT hr;

        if (ppvObject == nullptr)
            RETURN_HR(E_POINTER);

        if (pUnknown == nullptr)
        {
            *ppvObject = nullptr;
            return S_FALSE;
        }

        if (SUCCEEDED(hr = pUnknown->QueryInterface(GUID_UnwrapInterface, ppvObject)))
            return hr;

        *ppvObject = nullptr;
        return hr;
    }
}

#ifndef DECLARE_UUIDOF_HELPER
#define DECLARE_UUIDOF_HELPER(type, a, b, c, d, e, f, g, h, i, j, k) template<> inline constexpr GUID (::xcom::impl::guid_v<type>){a,b,c,{d,e,f,g,h,i,j,k}};
#endif

#define DECLARE_ABI_UUIDOF_HELPER(type, a, b, c, d, e, f, g, h, i, j, k) template<> inline constexpr GUID (::xcom::impl::guid_v<type<abi_t{}>>){a,b,c,{d,e,f,g,h,i,j,k}};
#define IMPLEMENT_STUB() ::xcom::StubHandler(__func__, __if_exists(this) { this } __if_not_exists(this) { nullptr })
#define IMPLEMENT_TODO() ::xcom::TodoHandler(__func__, __if_exists(this) { this } __if_not_exists(this) { nullptr })
