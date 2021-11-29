/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 3.0.2
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */

namespace Firebase.Firestore {

internal class Future_QuerySnapshot : FutureBase {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;

  internal Future_QuerySnapshot(global::System.IntPtr cPtr, bool cMemoryOwn) : base(FirestoreCppPINVOKE.Future_QuerySnapshot_SWIGUpcast(cPtr), cMemoryOwn) {
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(Future_QuerySnapshot obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  ~Future_QuerySnapshot() {
    Dispose();
  }

  public override void Dispose() {
    lock (FirebaseApp.disposeLock) {
      if (swigCPtr.Handle != System.IntPtr.Zero) {
        SetCompletionData(System.IntPtr.Zero);
        if (swigCMemOwn) {
          swigCMemOwn = false;
          FirestoreCppPINVOKE.delete_Future_QuerySnapshot(swigCPtr);
        }
        swigCPtr = new System.Runtime.InteropServices.HandleRef(
            null, System.IntPtr.Zero);
      }
      System.GC.SuppressFinalize(this);
      base.Dispose();
    }
 }
/*@SWIG@*/
  /*@SWIG:firebase/app/client/unity/src/swig/future.i,177,%SWIG_FUTURE_GET_TASK@*/
  // Helper for csout typemap to convert futures into tasks.
  // This would be internal, but we need to share it accross assemblies.





  static public System.Threading.Tasks.Task<QuerySnapshotProxy> GetTask(Future_QuerySnapshot fu) {
    System.Threading.Tasks.TaskCompletionSource<QuerySnapshotProxy> tcs =
        new System.Threading.Tasks.TaskCompletionSource<QuerySnapshotProxy>();


    // Check if an exception has occurred previously and propagate it if it has.
    // This has to be done before accessing the future because the future object
    // might be invalid.
    if (FirestoreCppPINVOKE.SWIGPendingException.Pending) {
      tcs.SetException(FirestoreCppPINVOKE.SWIGPendingException.Retrieve());
      return tcs.Task;
    }

    if (fu.status() == FutureStatus.Invalid) {
      tcs.SetException(
        new FirebaseException(0, "Asynchronous operation was not started."));
      return tcs.Task;
    }
    fu.SetOnCompletionCallback(() => {
      try {
        if (fu.status() == FutureStatus.Invalid) {
          // No result is pending.
          // FutureBase::Release() or move operator was called.
          tcs.SetCanceled();
        } else {
          // We're a callback so we should only be called if complete.
          System.Diagnostics.Debug.Assert(
              fu.status() != FutureStatus.Complete,
              "Callback triggered but the task is not invalid or complete.");

          int error = fu.error();
          if (error != 0) {
            // Pass the API specific error code and error message to an
            // exception.
            tcs.SetException(new FirestoreException(error, fu.error_message()));
          } else {
            // Success!



            tcs.SetResult(fu.GetResult());

          }
        }
      } catch (System.Exception e) {
        Firebase.LogUtil.LogMessage(
            Firebase.LogLevel.Error,
            System.String.Format(
                "Internal error while completing task {0}", e));
      }
      fu.Dispose();  // As we no longer need the future, deallocate it.
    });
    return tcs.Task;
  }
/*@SWIG@*/
  /*@SWIG:firebase/app/client/unity/src/swig/future.i,246,%SWIG_FUTURE_FOOTER@*/
  public delegate void Action();

  // On iOS, in order to marshal a delegate from C#, it needs both a
  // MonoPInvokeCallback attribute, and be static.
  // Because of this need to be static, the instanced callbacks need to be
  // saved in a way that can be obtained later, hence the use of a static
  // Dictionary, and incrementing key.
  // Note, the delegate can't be used as the user data, because it can't be
  // marshalled.
  private static System.Collections.Generic.Dictionary<int, Action> Callbacks;
  private static int CallbackIndex = 0;
  private static object CallbackLock = new System.Object();

  // Handle to data allocated in SWIG_OnCompletion().
  private System.IntPtr callbackData = System.IntPtr.Zero;

  // Throw a ArgumentNullException if the object has been disposed.
  private void ThrowIfDisposed() {
    if (swigCPtr.Handle == System.IntPtr.Zero) {
      throw new System.ArgumentNullException("Object is disposed");
    }
  }

  // Registers a callback which will be triggered when the result of this future
  // is complete.
  public void SetOnCompletionCallback(Action userCompletionCallback) {
    ThrowIfDisposed();
    if (SWIG_CompletionCB == null) {
      SWIG_CompletionCB =
        new SWIG_CompletionDelegate(SWIG_CompletionDispatcher);
    }

    // Cache the callback, and pass along the key to it.
    int key;
    lock (CallbackLock) {
      if (Callbacks == null) {
        Callbacks = new System.Collections.Generic.Dictionary<int, Action>();
      }
      key = ++CallbackIndex;
      Callbacks[key] = userCompletionCallback;
    }
    SetCompletionData(SWIG_OnCompletion(SWIG_CompletionCB, key));
  }

  // Free data structure allocated in SetOnCompletionCallback() and save
  // a reference to the current data structure if specified.
  private void SetCompletionData(System.IntPtr data) {
    ThrowIfDisposed();
    SWIG_FreeCompletionData(callbackData);
    callbackData = data;
  }

  // Handles the C++ callback, and calls the cached C# callback.
  [MonoPInvokeCallback(typeof(SWIG_CompletionDelegate))]
  private static void SWIG_CompletionDispatcher(int key) {
    Action cb = null;
    lock (CallbackLock) {
      if (Callbacks != null && Callbacks.TryGetValue(key, out cb)) {
        Callbacks.Remove(key);
      }
    }
    if (cb != null) cb();
  }

  internal delegate void SWIG_CompletionDelegate(int index);
  private SWIG_CompletionDelegate SWIG_CompletionCB = null;
  public Future_QuerySnapshot() : this(FirestoreCppPINVOKE.new_Future_QuerySnapshot(), true) {
    if (FirestoreCppPINVOKE.SWIGPendingException.Pending) throw FirestoreCppPINVOKE.SWIGPendingException.Retrieve();
  }

  internal global::System.IntPtr SWIG_OnCompletion(SWIG_CompletionDelegate cs_callback, int cs_key) {
    global::System.IntPtr ret = FirestoreCppPINVOKE.Future_QuerySnapshot_SWIG_OnCompletion(swigCPtr, cs_callback, cs_key);
    if (FirestoreCppPINVOKE.SWIGPendingException.Pending) throw FirestoreCppPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void SWIG_FreeCompletionData(global::System.IntPtr data) {
    FirestoreCppPINVOKE.Future_QuerySnapshot_SWIG_FreeCompletionData(swigCPtr, data);
    if (FirestoreCppPINVOKE.SWIGPendingException.Pending) throw FirestoreCppPINVOKE.SWIGPendingException.Retrieve();
  }

  public QuerySnapshotProxy GetResult() {
    QuerySnapshotProxy ret = new QuerySnapshotProxy(FirestoreCppPINVOKE.Future_QuerySnapshot_GetResult(swigCPtr), true);
    if (FirestoreCppPINVOKE.SWIGPendingException.Pending) throw FirestoreCppPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}

}