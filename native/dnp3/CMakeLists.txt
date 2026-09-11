cmake_minimum_required(VERSION 3.20)

project(EliteScadaDnp3Host LANGUAGES CXX)

include(FetchContent)

set(DNP3_TLS OFF CACHE BOOL "" FORCE)
set(DNP3_TESTS OFF CACHE BOOL "" FORCE)
set(DNP3_EXAMPLES OFF CACHE BOOL "" FORCE)
set(DNP3_FUZZING OFF CACHE BOOL "" FORCE)
set(DNP3_JAVA OFF CACHE BOOL "" FORCE)
set(DNP3_EVERYTHING OFF CACHE BOOL "" FORCE)
set(DNP3_STATIC_LIBS ON CACHE BOOL "" FORCE)

if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>" CACHE STRING "" FORCE)
endif()

FetchContent_Declare(
    opendnp3
    GIT_REPOSITORY https://github.com/dnp3/opendnp3.git
    GIT_TAG 26b4c01e4839bbbda8866655e086471c4917ee53
    GIT_SHALLOW FALSE
    GIT_PROGRESS TRUE
)
FetchContent_MakeAvailable(opendnp3)

add_executable(EliteScada.Dnp3Host main.cpp)
target_compile_features(EliteScada.Dnp3Host PRIVATE cxx_std_17)
target_link_libraries(EliteScada.Dnp3Host PRIVATE opendnp3)

if(MSVC)
    target_compile_options(EliteScada.Dnp3Host PRIVATE /W4)
else()
    target_compile_options(EliteScada.Dnp3Host PRIVATE -Wall -Wextra -Wpedantic)
endif()

install(TARGETS EliteScada.Dnp3Host RUNTIME DESTINATION native/dnp3)
