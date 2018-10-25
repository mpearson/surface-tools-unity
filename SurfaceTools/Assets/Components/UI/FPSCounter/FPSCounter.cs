using System;
using UnityEngine;
using UnityEngine.UI;

namespace Doublemice.UI {
  public class FPSCounter : BetterMonoBehaviour {
    public float fpsMeasurePeriod = 0.2f;

    Text fpsBox;
    float fpsAccumulator = 0;
    float fpsNextPeriod = 0;
    float currentFps;
    // const string display = "{0} FPS";

    public override void Start() {
      this.fpsBox = GetComponent<Text>();
      this.fpsNextPeriod = Time.realtimeSinceStartup + this.fpsMeasurePeriod;

      base.Start();
    }


    public void Update() {
      // measure average frames per second
      this.fpsAccumulator++;
      if (Time.realtimeSinceStartup > this.fpsNextPeriod) {
        this.currentFps = (this.fpsAccumulator / this.fpsMeasurePeriod);
        this.fpsAccumulator = 0;
        this.fpsNextPeriod += this.fpsMeasurePeriod;
        this.fpsBox.text = this.currentFps.ToString(); //string.Format(display, currentFps);
      }
    }
  }
}
