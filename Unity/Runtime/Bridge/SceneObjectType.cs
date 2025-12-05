// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
namespace Miris.Runtime
{
#endif

#if __cplusplus
public enum class SceneObjectType 
#else
public enum SceneObjectType 
#endif
{ AssetRootObject, SceneObject, StreamObject, GaussianSplats, LodCollection, PointsObject, Camera };


#if USING_CSHARP
} // Miris.Runtime
#else
;
#undef public
#endif
