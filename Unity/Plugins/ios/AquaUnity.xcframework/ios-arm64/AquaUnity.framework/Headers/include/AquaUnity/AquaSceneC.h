// Copyright (c) 2024 Miris Inc. All rights reserved.

#pragma once

#include <AquaClient/FrameInfo.h>
#include <AquaClient/ImageData.h>
#include <AquaClient/LodRefinementParameters.h>
#include <AquaClient/NativeArray.h>
#include <AquaClient/ServerEnvironment.h>
#include <AquaFoundation/AquaObject.h>
#include <AquaScene/AssetMetadata.h>
#include <AquaScene/AttributeInfo.h>
#include <AquaScene/SceneChangeIds.h>

#define AQUA_INVALID_HANDLE ((void*)0)

struct AquaClient;
typedef struct AquaClient* AquaClientHandle;

extern "C" {

void SetLogLevel(aqua::LogLevel logLevel);
aqua::LogLevel GetLogLevel();

int GetPlatform();
int LibAquaIsDebug();

AquaClientHandle CreateClient();
void DestroyClient(AquaClientHandle client);

// ---------------------------------------------------------------
// Asset Management API
// ---------------------------------------------------------------

/// Set the key that the SDK will include with every request relevant to fetching assets.
/// This function should be called once before `GetAssets` is called.
void SetAssetViewerKey(AquaClientHandle client, const char* key);

/// Retrieve all available assets from the server environment.
/// An optional array of tags can be provided to filter the assets that are retrieved.
/// The tags are combined using the AND operator, i.e. they are exclusive filters.
/// A `callback` must be provided which will be invoked and supplied with the scene data when it is ready.
/// This callback function pointer must remain valid until it is invoked once.
/// The `userData` is opaque to the SDK and will simply be passed to the callback.
void GetAssets(AquaClientHandle client, const char** tags, int tagsCount, aqua::FillNativeArrayCallback callback,
               void* userData);

/// Get all available server environment names.
/// The callback is called with `ptr` being a pointer to an array of `const char*`.
void GetAvailableEnvironments(AquaClientHandle client, aqua::FillNativeArrayCallback callback, void* userData);

/// Get the internally defined default server environment name.
const char* GetDefaultEnvironment(AquaClientHandle client);

/// Changes the server environment from which assets are to be fetched to the one with the given name.
/// The callback is invoked with the result of whether the change completed successfully or not.
void SetServerEnvironment(AquaClientHandle client, const char* environmentName,
                          aqua::SetServerEnvironmentCallback callback, void* userData);

// ---------------------------------------------------------------
// Utility APIs
// ---------------------------------------------------------------

/// Fetch the image at the given URL and obtain its raw pixel buffer. Currently only PNG and JPG images are supported.
/// A `callback` must be provided which must remain valid until it is invoked once.
void GetImageFromUrl(AquaClientHandle client, const char* url, aqua::GetImagePixelBufferCallback callback,
                     void* userData);

/// Prefetch the resource at the given URL and cache the response.
uint64_t PrefetchContent(AquaClientHandle client, const char* url);

// ---------------------------------------------------------------
// Scene API
// ---------------------------------------------------------------

/// Clear the aqua scene.  This creates a new scene in-place on the AquaScene.
void ClearScene(AquaClientHandle client);

/// Trigger an update to the scene execution, which launches async operators.
/// Must be called outside of the SceneChangeTracker scene lock.
void UpdateSceneExecution(AquaClientHandle client);

/// Block the current thread until ALL scene operators have completed executing.
/// If this is called from the main thread, do NOT call this within the scope of a scene lock
/// as the scene operators will also need to acquire that lock to complete their work.
/// This is also useful in tests to block until the async operators have completed before
/// checking the results.
void WaitForSceneExecution(AquaClientHandle client);

/// Cancel all scene operators currently in the execution queue.
void CancelAllSceneExecution(AquaClientHandle client);

/// Add an addressable stream to the scene.
int AddStream(AquaClientHandle client, char* streamName, char* contentUrl,
              aqua::SceneClient clientType = aqua::SceneClient::General);

/// Removes an addressable stream from the scene.
bool RemoveStream(AquaClientHandle client, int streamObjectId);

/// Set a Unity camera's local-to-world matrix onto our Aqua scene's main camera -- this information
/// is used by the scene operators to progressively refine the renderable LODs.
void SetMainCameraTransform(AquaClientHandle client, float* cameraTransform);

/// Set the current height/y offset of the floor of the xr scene from Unity
void SetXRFloorHeight(AquaClientHandle client, float height);

/// Set a scene object's transformation matrix.
void SetSceneObjectTransform(AquaClientHandle client, int sceneObjectId, float* transform);

/// Set the parameters that control how the LOD refinement behaves.
void SetLodRefinementParameters(AquaClientHandle client, const aqua::LodRefinementParameters* refinementParameters);

/// Acquire or release the scene lock, such that we can query changed state from Unity in a thread-safe manner
/// (won't have to worry about async operators populating addition content).
bool LockScene(AquaClientHandle client);
void UnlockScene(AquaClientHandle client);

/// Query the counts of the scene change buffers such that we can allocate managed arrays on the C# side and populate them in
/// the proceeding GetSceneChanges call.
/// TODO: Change this to populate a struct that we can pass in instead so there aren't a bajillion parameters.
/// The two functions below MUST be called within the scope of LockScene(AquaClientHandle client) and UnlockScene(AquaClientHandle client)
void GetSceneChangesCounts(AquaClientHandle client, aqua::SceneChangeIds* sceneChangeIds);

void GetSceneChanges(AquaClientHandle client, aqua::SceneChangeIds* sceneChangeIds);

/// Get scene-wide LOD min & max indices (AquaClientHandle client, for mapping meaningful colors in our LOD heat map).
void GetLodMinMaxIndices(AquaClientHandle client, int* minLodIndex, int* maxLodIndex);

/// Get scene meta data
void GetSceneMetadata(AquaClientHandle client, aqua::SceneMetadata* metadata);

// Scene Object API
void PrintSceneObjectHierarchy(AquaClientHandle client, int sceneObjectId);
int GetSceneRootObjectId(AquaClientHandle client);
int GetSceneObjectChildrenCount(AquaClientHandle client, int sceneObjectId);
void GetSceneObjectChildren(AquaClientHandle client, int sceneObjectId, int* childIds);
int GetSceneObjectParent(AquaClientHandle client, int sceneObjectId);
int GetSceneObjectType(AquaClientHandle client, int sceneObjectId);
char* GetSceneObjectName(AquaClientHandle client, int sceneObjectId);
std::string GetSceneObjectPath(AquaClientHandle handle, int sceneObjectId);
void SceneObjectExport(AquaClientHandle client, int sceneObjectId, char* outputPath, bool asBinary);
void GetBufferHash(AquaClientHandle client, int sceneObjectId, const char* bufferName, uint64_t* outHash);
void GetBoundingBox(AquaClientHandle client, int sceneObjectId, float* boxData);
void GetTransform(AquaClientHandle client, int sceneObjectId, float* transform);
void GetMetadata(AquaClientHandle client, int sceneObjectId, aqua::AssetMetadata* metadata);
int GetAttributeCount(AquaClientHandle client, int sceneObjectId);
bool HasAttribute(AquaClientHandle client, int sceneObjectId, const char* attributeName);
void GetAttribute(AquaClientHandle client, int sceneObjectId, const char* attributeName,
                  aqua::AttributeInfo* attributeInfo);
void GetMosaicDescriptors(void* mosaicDescriptorPtr, aqua::MosaicDescriptorInfo* mosaicDescriptor);

void* GetRenderEventCallbackPtr();

// TeleportArea API
void GetTeleportAreaDataSizes(AquaClientHandle client, int sceneObjectId, int* vertexCount, int* triangleCount);
void GetTeleportAreaData(AquaClientHandle client, int sceneObjectId, float* vertexData, int* triangleData);

// Camera API
float GetFieldOfView(AquaClientHandle client, int sceneObjectId);

int GetLodIndex(AquaClientHandle client, int sceneObjectId);

/// Record the current frame's information from captured from Unity, into Aqua.
void RecordFrameInfo(AquaClientHandle client, const aqua::FrameInfo* frameInfo);

/// Query the current number of scene operators in the execution queue.
int GetSceneOperatorCount(AquaClientHandle client);

void PerformThroughputTest(AquaClientHandle client, const char* payload, const char* deviceId);

void SetUsdPath(AquaClientHandle client, const char* payload);

} // extern "C"
