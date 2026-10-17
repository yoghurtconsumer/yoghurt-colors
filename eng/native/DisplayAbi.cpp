// Compile-only ABI checks against installed Windows SDK headers. No display APIs run.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstddef>
static_assert(sizeof(void*) == 8, "Use the x64 Developer PowerShell");
static_assert(sizeof(DEVMODEW) == 220);
static_assert(offsetof(DEVMODEW, dmFields) == 72);
static_assert(offsetof(DEVMODEW, dmPelsWidth) == 172);
static_assert(offsetof(DEVMODEW, dmDisplayFrequency) == 184);
static_assert(sizeof(DISPLAYCONFIG_DEVICE_INFO_HEADER) == 20);
static_assert(sizeof(DISPLAYCONFIG_PATH_SOURCE_INFO) == 20);
static_assert(sizeof(DISPLAYCONFIG_PATH_TARGET_INFO) == 48);
static_assert(sizeof(DISPLAYCONFIG_PATH_INFO) == 72);
static_assert(offsetof(DISPLAYCONFIG_PATH_INFO, targetInfo) == 20);
static_assert(offsetof(DISPLAYCONFIG_PATH_INFO, flags) == 68);
static_assert(sizeof(DISPLAYCONFIG_SOURCE_MODE) == 20);
static_assert(sizeof(DISPLAYCONFIG_VIDEO_SIGNAL_INFO) == 48);
static_assert(sizeof(DISPLAYCONFIG_MODE_INFO) == 64);
static_assert(offsetof(DISPLAYCONFIG_MODE_INFO, targetMode) == 16);
static_assert(sizeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO) == 32);
static_assert(sizeof(DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE) == 24);
