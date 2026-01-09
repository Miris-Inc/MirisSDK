// Copyright © 2025 Miris, Inc. All rights reserved.

// defines for indirect draw call elements
#pragma once

#define IND_DRAW_PARAMS_COUNT_PER_INSTANCE 0
#define IND_DRAW_PARAMS_TOTAL_INSTANCE_COUNT 1
#define IND_DRAW_PARAMS_START_INDEX_LOCATION 2
#define IND_DRAW_PARAMS_BASE_VERTEX_LOCATION 3
#define IND_DRAW_PARAMS_START_INSTANCE_LOCATION 4

#define IND_DISPATCH_THREADGROUPS_X_DIRECTION 0
#define IND_DISPATCH_THREADGROUPS_Y_DIRECTION 1
#define IND_DISPATCH_THREADGROUPS_Z_DIRECTION 2
#define IND_DISPATCH_PADDING 3

static const float MAX_VIEW_DISTANCE = 100.0f;
static const float MAX_ALPHA_VALUE = 0.99f;

static const float2 quadPositionsInClipSpace[6]={
                                                float2(-1,-1),
                                                float2(1,-1),
                                                float2(-1,1),
                                                float2(-1,1),
                                                float2(1,-1),
                                                float2(1,1)
                                                };
