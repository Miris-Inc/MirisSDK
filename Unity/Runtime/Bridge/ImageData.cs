// This is a valid C++ and C# file :)

#if __cplusplus
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
using System.Runtime.InteropServices;
namespace Miris.Runtime
{
#endif

#if __cplusplus
    using GetImagePixelBufferCallback = void(*)(int width, int height, int bytesPerPixel, void* pixelData, void* userData);
#else
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void GetImagePixelBufferCallback(int width, int height, int bytesPerPixel, System.IntPtr pixelData, System.IntPtr userData);
#endif

#if USING_CSHARP
}
#endif
