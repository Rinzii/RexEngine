#pragma once

#if defined(REX_NATIVE_STATIC)
    #define REX_NATIVE_API
#elif defined(_WIN32) || defined(__CYGWIN__)
    #if defined(REX_NATIVE_EXPORTS)
        #define REX_NATIVE_API __declspec(dllexport)
    #else
        #define REX_NATIVE_API __declspec(dllimport)
    #endif
#elif defined(__GNUC__) || defined(__clang__)
    #define REX_NATIVE_API __attribute__((visibility("default")))
#else
    #define REX_NATIVE_API
#endif

#ifndef REX_EXTERN
#define REX_EXTERN extern "C"
#endif

