// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP
namespace Aqua.Runtime
{
#endif

#if __cplusplus
public enum class SceneObjectType 
#else
public enum SceneObjectType 
#endif
{ AssetRootObject, SceneObject, StreamObject, GaussianSplats, LodCollection, PointsObject, TeleportArea, Camera };


#if USING_CSHARP
} // Aqua.Runtime
#else
;
#undef public
#endif
