# Build libzmq for Zebus

Download the latest release of [libzmq](https://github.com/zeromq/libzmq/releases) and extract it.

## Windows

Open a *Developer Command Prompt for VS 2017* inside the source directory.

### x64

```
mkdir build_x64
cd build_x64
cmake -G "Visual Studio 15 2017 Win64" -D WITH_PERF_TOOL=OFF -D ZMQ_BUILD_TESTS=OFF -D ENABLE_CPACK=OFF -D CMAKE_BUILD_TYPE=Release ..
msbuild /m /v:m /p:Configuration=Release /p:Platform=x64 ZeroMQ.sln
```

The dynamic library will be outputted in `bin\Release\libzmq-*.dll`.

### x86

```
mkdir build_x86
cd build_x86
cmake -G "Visual Studio 15 2017" -D WITH_PERF_TOOL=OFF -D ZMQ_BUILD_TESTS=OFF -D ENABLE_CPACK=OFF -D CMAKE_BUILD_TYPE=Release ..
msbuild /m /v:m /p:Configuration=Release /p:Platform=Win32 ZeroMQ.sln
```

The dynamic library will be outputted in `bin\Release\libzmq-*.dll`.

## GNU+Linux

Open a shell inside the source directory.

### amd64

```
mkdir build_amd64
cd build_amd64
cmake -DWITH_PERF_TOOL=OFF -DZMQ_BUILD_TESTS=OFF -DCMAKE_BUILD_TYPE=Release ..
make -j$[$(nproc)+1]
strip lib/libzmq.so.*.*
```

The dynamic library will be outputted in `lib/libzmq.so.*.*`.

### i386

```
mkdir build_i386
cd build_i386
cmake -DWITH_PERF_TOOL=OFF -DZMQ_BUILD_TESTS=OFF -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS=-m32 -DCMAKE_CXX_FLAGS=-m32 ..
make -j$[$(nproc)+1]
strip lib/libzmq.so.*.*
```

The dynamic library will be outputted in `lib/libzmq.so.*.*`.
