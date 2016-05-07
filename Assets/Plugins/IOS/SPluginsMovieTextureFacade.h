#import <Foundation/Foundation.h>
#import "SPluginsLog.h"
#import "Player/SPluginsMovieTexturePlayer.h"


typedef void (*DelegateOnLoadCompleted)(int nativeTextureID_, int resultType_, const char*);
typedef void (*DelegateOnPlayCompleted)(int nativeTextureID_, int completedType_);

@interface SPluginsMovieTextureFacade : NSObject <DelegateOnEventListener>
{
@private
	NSMutableDictionary* _playerDic;
	DelegateOnLoadCompleted _delegateOnLoadCompleted;
	DelegateOnPlayCompleted _delegateOnPlayCompleted;
}

- (void)onLoadCompleted:(int)nativeTextureID_ resultType:(RESULT_TYPE)resultType_ description:(NSString*)description_;
- (void)onPlayCompleted:(int)nativeTextureID_ completedType:(PLAY_COMPLETED_TYPE)completedType_;
@end


extern void UORegisterLogDelegates(DelegateOnLog delegateLog_,
								 DelegateOnLog delegateWarning_,
								 DelegateOnLog delegateError_);
extern void UOEnableLog(bool enable_);

extern void UOLoadAsync(int nativeTextureID_, const char* fullPathRelativeToTheStreamingAssets_, DelegateOnLoadCompleted delegateOnLoadCompleted_);
extern void UOLoadAsyncAtAbsolutePath(int nativeTextureID_, const char* fullPath_, DelegateOnLoadCompleted delegateOnLoadCompleted_);
extern void UOBindTexture(int nativeTextureID_);
extern void UORenderObject(int nativeTextureID_);
extern void UOUpdate(int nativeTextureID_);
extern void UOPlay(int nativeTextureID_, DelegateOnPlayCompleted delegateOnPlayCompleted_);
extern void UOStop(int nativeTextureID_);
extern void UOPause(int nativeTextureID_);
extern void UOResume(int nativeTextureID_);
extern void UOSeekTo(int nativeTextureID_, int timeMS_);
extern void UOSetVolume(int nativeTextureID_, float normalizedVolume_);
extern void UOSet3DSoundVolumeMag(int nativeTextureID_, float volumeMagByDistance_, float normalizedAudioPan);
extern void UOSetEnable3DSound(int nativeTextureID_, bool enable_);
extern void UOSetLooping(int nativeTextureID_, bool loop_);
extern int UOGetCurrentTimeMS(int nativeTextureID_);
extern int UOGetDurationTimeMS(int nativeTextureID_);
extern bool UOIsPaused(int nativeTextureID_);
extern bool UOIsPlaying(int nativeTextureID_);
extern bool UOIsLooping(int nativeTextureID_);
