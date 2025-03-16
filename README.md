# XDL Compiler

XDL is a definition language similar to Microsoft IDL but with versioning support and less strict rules.

### Usage

```bash
xdl <input_file>.xdl <output_dir> [factory_name]
```

`input_file` : the name of the input xdl file

`output_dir` : the folder where headers are generated into

`factory_name` : the name of the factory function that would create a type instance given a type and an ABI version, if no factory name is specified then no factory function is generated

The factory function is defined like this

```cpp
template<template<abi_t> typename T>
inline HRESULT FactoryFunctionName(abi_t ABI, void **ppvObject)
```

and can be used like this

```cpp
abi_t abi;
abi.Major = 10;
abi.Minor = 0;
abi.Build = 15063;
abi.Revision = 0;

IVersionedClassFactory* pClass;
HRESULT hr = FactoryFunctionName<VersionedClassFactory>(abi, (void**)&pClass);
```

The factory class can be defined like this

```csharp
[no_uuid]
interface IVersionedClassFactory
{
    HRESULT CreateMyVersionedClass([out] void** ppClass);
};

[force_abi]
class VersionedClassFactory : IVersionedClassFactory
{

};
```

and the implementation of `CreateMyVersionedClass` can look like this

```cpp
HRESULT CreateMyVersionedClass(void** ppClass)
{
    *ppClass = new MyVersionedClass<ABI>();
}
```

### TODO

- [ ] Support full C declarator syntax (including function pointers)
- [ ] Recognize annotations and translate them to SAL on the C++ types
- [ ] Support for defining constants
- [ ] Support for simple expressions with constants (i.e. `1 + 1`)
- [ ] Proper error checking and better error messages
- [ ] Support for optionally generating headers for imports
- [ ] Support for multiple XDL file inputs

### Examples

The following example showcases how to define a simple interface that makes use of basic XDL versioning features

```csharp
import "unknown.xdl";

namespace simple_example
{
    [uuid("f0d7fd9c-33a3-4fc3-abe1-511c8917dbf6")]
    interface ISimpleClass : IUnknown
    {
        [removed(10,0,15063,0)]
        UINT m_StringsCount;
        
        [added(10,0,15063,0)]
        UINT64 m_StringsCount;
        
        [removed(10,0,15063,0)]
        UINT AddString([in] LPCSTR pString);
        
        [added(10,0,15063,0)]
        UINT64 AddString([in] LPCWSTR lpString);
        
        [removed(10,0,15063,0)]
        UINT GetString([out] LPCSTR pString, [in] UINT size, [in] UINT index);
        
        [added(10,0,15063,0)]
        UINT64 GetString([out] LPCWSTR lpString, [in] UINT64 size, [in] UINT64 index);
    };
    
    class SimpleClass : ISimpleClass
    {
        
    };
}
```

The following example showcases how to define complex interfaces/classes that make use of more advanced XDL versioning features

```csharp
import "unknown.xdl";

namespace gfx
{
    struct GraphicsClassDesc
    {
        UINT TexturesCapacity;
        [added(6,2,9200,0)] UINT ShadersCapacity;
        [added(10,0,15063,0), removed(10,0,22000,0)] UINT ViewsCapacity;
        [removed(10,0,18362,0)] UINT OpaqueDataCapacity;
    };

    [uuid("b54c4d35-8172-4575-9362-a614d34a834b")]
    interface IGraphicsClass : IGraphicsUnknown
    {
        GraphicsClassDesc m_Desc;
        
        HRESULT Initialize([in] IUnknown* pGraphicsDevice);
        void GetDevice([out] IUnknown** ppGraphicsDevice);
        
        [added(10,0,22000,0)]
        HRESULT UpdateDevice(IUnknown* pGraphicsDevice);
    };
    
    class SomeGraphicsClass : IGraphicsClass
    {
        
    };
    
    enum MemoryPressure
    {
        Low,
        Medium,
        High
    };
    
    struct MemoryInfo
    {
        UINT64 TotalAllocated;
        UINT64 TotalFreed;
        
        [conditional("defined(TRACK_MEMORY_PRESSURE)")]
        MemoryPressure Pressure;
    };
    
    [uuid("1bc4eedb-9043-47ad-9572-590d1e50771b")]
    interface IMemoryManager : IUnknown
    {
        HRESULT AllocateMemory([in] UINT64 size, [out] void** ppMemory);
        
        [removed(10,0,22000,0)]
        UINT64 GetTotalAllocatedMemory();
        
        [removed(10,0,22000,0)]
        UINT64 GetTotalFreedMemory();
        
        [removed(10,0,22000,0)]
        [conditional("defined(TRACK_MEMORY_PRESSURE)")]
        MemoryPressure GetMemoryPressure();
        
        [added(10,0,22000,0)]
        void GetMemoryInfo([out] MemoryInfo* pMemoryInfo);
    };
    
    [uuid("a97f9933-9696-423d-9e2a-44c9cefbd058")]
    interface IMemoryManager2 : [removed(10,0,26100,0)] IUnknown, [added(10,0,26100,0)] IMemoryManager
    {
        HRESULT FreeMemory([in] void* ppMemory);
    };
    
    class MemoryManager : [removed(10,0,26100,0)] IMemoryManager, [added(10,0,22631,0)] IMemoryManager2
    {
        
    };
}
```
