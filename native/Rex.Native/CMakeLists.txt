cmake_minimum_required(VERSION 3.25)
project(RexNative LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(FetchContent)
include(CTest)

option(REX_BUILD_SHARED "Build rex_native as a shared library" ON)

if(NOT DEFINED REX_OUTPUT_DIR)
    set(REX_OUTPUT_DIR "${CMAKE_BINARY_DIR}/stage")
endif()

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${REX_OUTPUT_DIR}")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${REX_OUTPUT_DIR}")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${REX_OUTPUT_DIR}")

file(GLOB_RECURSE REX_NATIVE_SOURCES CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cxx"
    "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cc"
)

if(REX_BUILD_SHARED)
    add_library(rex_native SHARED ${REX_NATIVE_SOURCES})
else()
    add_library(rex_native STATIC ${REX_NATIVE_SOURCES})
endif()

target_include_directories(rex_native
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

if(REX_BUILD_SHARED)
    target_compile_definitions(rex_native
        PRIVATE
            REX_NATIVE_EXPORTS
    )
else()
    target_compile_definitions(rex_native
        PUBLIC
            REX_NATIVE_STATIC
    )
endif()

set_target_properties(rex_native PROPERTIES
    OUTPUT_NAME rex_native
)

FetchContent_Declare(
    SDL3
    GIT_REPOSITORY https://github.com/libsdl-org/SDL.git
    GIT_TAG main
)
set(SDL_SHARED ON CACHE BOOL "" FORCE)
set(SDL_STATIC OFF CACHE BOOL "" FORCE)
set(SDL_TEST_LIBRARY OFF CACHE BOOL "" FORCE)
set(SDL_TESTS OFF CACHE BOOL "" FORCE)
set(SDL_EXAMPLES OFF CACHE BOOL "" FORCE)
set(SDL_DISABLE_INSTALL ON CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(SDL3)

target_link_libraries(rex_native
    PUBLIC
        SDL3::SDL3
)

if(BUILD_TESTING)
    FetchContent_Declare(
        googletest
        URL https://github.com/google/googletest/archive/refs/heads/main.zip
    )
    set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
    FetchContent_MakeAvailable(googletest)

    file(GLOB_RECURSE REX_NATIVE_TEST_SOURCES CONFIGURE_DEPENDS
        "${CMAKE_CURRENT_SOURCE_DIR}/tests/*.cpp"
        "${CMAKE_CURRENT_SOURCE_DIR}/tests/*.cxx"
        "${CMAKE_CURRENT_SOURCE_DIR}/tests/*.cc"
    )

    if(REX_NATIVE_TEST_SOURCES)
        add_executable(rex_native_tests ${REX_NATIVE_TEST_SOURCES})

        target_include_directories(rex_native_tests
            PRIVATE
                ${CMAKE_CURRENT_SOURCE_DIR}/include
        )

        target_link_libraries(rex_native_tests
            PRIVATE
                rex_native
                GTest::gtest_main
        )

        include(GoogleTest)
        gtest_discover_tests(rex_native_tests)
    endif()
endif()
