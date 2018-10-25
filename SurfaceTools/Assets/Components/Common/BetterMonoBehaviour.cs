using System.Collections;
using UnityEngine;

namespace Doublemice {
  /// <summary>
  /// Fixes the idiotic execution order of <see cref="OnEnable()"/> before <see cref="Start()"/>.
  /// Defines <see cref="OnEnableAfterStart()"/>, which is called at the end of <see cref="Start()"/>
  /// and any time <see cref="OnEnable()"/> is called afterwards.
  /// Also provides <see cref="OnDestroyBeforeQuit()"/> and <see cref="OnDisableBeforeQuit"/>
  /// which is not called when the application is closing. Prevents moronic NullReferenceExceptions.
  /// </summary>
  public class BetterMonoBehaviour : MonoBehaviour {
    protected bool startCalled = false;
    protected bool applicationQuit = false;

    public virtual void Start() {
      this.startCalled = true;

      this.OnEnableAfterStart();
    }

    public virtual void OnEnable() {
      if (this.startCalled)
        this.OnEnableAfterStart();
    }

    public virtual void OnEnableAfterStart() { }

    public void OnApplicationQuit() {
      this.applicationQuit = true;
    }

    public void OnDisable() {
      if (!this.applicationQuit)
        this.OnDisableBeforeQuit();
    }

    public virtual void OnDisableBeforeQuit() { }

    public virtual void OnDestroy() {
      if (!this.applicationQuit)
        this.OnDestroyBeforeQuit();
    }

    public virtual void OnDestroyBeforeQuit() { }
  }
}
