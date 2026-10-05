using UnityEngine;
namespace OnAir {
 public static class WindowNightSchedule {
  public const float SleepStartSeconds=120;
  public const float FinalLitFraction=.10f;
  // Automatic night is solar phase .5..1 (300 seconds by default, including blue hour).
  public static float Progress(float phase,float nightSeconds,bool automatic){
   if(!automatic)return 0;
   if(phase<.08f)return 1; // Keep late-night windows asleep through dawn; never relight at phase wrap.
   if(phase<.5f)return 0;
   float duration=Mathf.Max(30,nightSeconds),age=(phase-.5f)*2*duration;
   float start=Mathf.Min(SleepStartSeconds,duration*.8f);
   return Mathf.InverseLerp(start,duration,age);
  }
  public static float LitFraction(float progress)=>Mathf.Lerp(1,FinalLitFraction,Mathf.Clamp01(progress));
 }
}