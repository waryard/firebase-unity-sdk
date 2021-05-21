/*
 * Copyright 2017 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Storage.Internal;

namespace Firebase.Storage {
  /// <summary>Represents a reference to a Google Cloud Storage object.</summary>
  /// <remarks>
  ///   Represents a reference to a Google Cloud Storage object. Developers can upload and download
  ///   objects, get/set object metadata, and delete an object at a specified path.
  ///   (see <a href="https://cloud.google.com/storage/">Google Cloud Storage</a>)
  /// </remarks>
  public sealed class StorageReference {

    /// <summary>
    /// Extracts the cancellation status and exception from a task generated by a
    /// firebase::Future.
    /// </summary>
    private class TaskCompletionStatus {

      /// <summary>
      /// true if the task completed successfully.
      /// </summary>
      public bool IsSuccessful { get; private set; }

      /// <summary>
      /// true if the task was canceled.
      /// </summary>
      public bool IsCanceled { get; private set; }

      /// <summary>
      /// Non-null if the task failed.
      /// </summary>
      public System.Exception Exception { get; private set; }

      /// <summary>
      /// Extract the completion status from the specified task.
      /// </summary>
      /// <param name="task">Task to extract completion status from.</param>
      /// <param name="operationDescription">Description of the operation performed by the
      /// task used to log task status.</param>
      /// <param name="logger">Used to log task completion status.</param>
      public TaskCompletionStatus(Task task, string operationDescription, ModuleLogger logger) {
        if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled) {
          IsSuccessful = true;
          logger.LogMessage(LogLevel.Debug, String.Format("{0} completed successfully.",
                                                          operationDescription));
          return;
        }
        StorageException storageException =
          task.IsFaulted ? StorageException.CreateFromException(task.Exception) : null;
        // When an operation is canceled a C++ future will complete with a cancellation error code
        // so cancel the task when the cancelation error code is detected.
        if (task.IsCanceled || (storageException != null &&
                                storageException.ErrorCode == StorageException.ErrorCanceled)) {
          logger.LogMessage(LogLevel.Debug, String.Format("{0} canceled.", operationDescription));
          IsCanceled = true;
          return;
        }

        Exception = storageException;
        if (Exception != null) {
          logger.LogMessage(LogLevel.Debug, String.Format("{0} failed {1}.", operationDescription,
                                                          Exception.Message));
          return;
        }
        logger.LogMessage(LogLevel.Debug, String.Format("{0} failed due to an unknown error.",
                                                        operationDescription));
        Exception = new InvalidOperationException(String.Format("{0} failed.",
                                                                operationDescription));
      }

      /// <summary>
      /// Create a task from this object's status.
      /// </summary>
      /// <returns>A completed Task from this object's status.</returns>
      public Task ToTask() {
        var taskCompletionSource = new TaskCompletionSource<bool>();
        if (IsSuccessful) {
          taskCompletionSource.SetResult(true);
        } else if (IsCanceled) {
          // Emulates Task.FromCanceled in .NET 4.5.
          taskCompletionSource.SetCanceled();
        } else {
          // Emulates Task.FromException in .NET 4.5.
          taskCompletionSource.SetException(this.Exception);
        }
        return taskCompletionSource.Task;
      }
    }

    // Storage object this reference was created from.
    private readonly FirebaseStorage firebaseStorage;

    /// <summary>
    /// Construct a wrapper around the StorageReferenceInternal object.
    /// </summary>
    internal StorageReference(FirebaseStorage storage,
                              StorageReferenceInternal storageReferenceInternal) {
      firebaseStorage = storage;
      Internal = storageReferenceInternal;
      Logger = new ModuleLogger(parentLogger: firebaseStorage.Logger) {
        Tag = String.Format("StorageReference {0}", ToString())
      };
    }

    /// <summary>
    /// Logger for this reference.
    /// </summary>
    // Logger for this reference.
    internal ModuleLogger Logger { get; private set; }

    /// <summary>
    ///   Returns a new instance of
    ///   <see cref="StorageReference" />
    ///   pointing to the parent location or null
    ///   if this instance references the root location.
    ///   For example:
    ///   <pre>
    ///     <c>
    ///       path = foo/bar/baz   parent = foo/bar
    ///       path = foo           parent = (root)
    ///       path = (root)        parent = (null)
    ///     </c>
    ///   </pre>
    /// </summary>
    /// <returns>
    ///   the parent
    ///   <see cref="StorageReference" />
    ///   .
    /// </returns>
    public StorageReference Parent {
      get { return new StorageReference(firebaseStorage, Internal.GetParent()); }
    }

    /// <summary>
    ///   Returns a new instance of
    ///   <see cref="StorageReference" />
    ///   pointing to the root location.
    /// </summary>
    /// <returns>
    ///   the root
    ///   <see cref="StorageReference" />
    ///   .
    /// </returns>
    public StorageReference Root { get { return firebaseStorage.RootReference; } }

    /// <summary>Returns the short name of this object.</summary>
    /// <returns>the name.</returns>
    public string Name { get { return Internal.Name; } }

    /// <summary>
    ///   Returns the full path to this object, not including the Google Cloud Storage bucket.
    /// </summary>
    /// <returns>the path.</returns>
    public string Path { get { return Internal.FullPath; } }

    /// <summary>Return the Google Cloud Storage bucket that holds this object.</summary>
    /// <returns>the bucket.</returns>
    public string Bucket { get { return Internal.Bucket; } }

    /// <summary>
    ///   Returns the
    ///   <see cref="FirebaseStorage" />
    ///   service which created this reference.
    /// </summary>
    /// <returns>
    ///   The
    ///   <see cref="FirebaseStorage" />
    ///   service.
    /// </returns>
    public FirebaseStorage Storage { get { return firebaseStorage; } }

    /// <summary>
    ///   Returns a new instance of
    ///   <see cref="StorageReference" />
    ///   pointing to a child location of the current
    ///   reference. All leading and trailing slashes will be removed, and consecutive slashes will
    ///   be compressed to single slashes. For example:
    ///   <pre>
    ///     <c>
    ///       child = /foo/bar     path = foo/bar
    ///       child = foo/bar/     path = foo/bar
    ///       child = foo///bar    path = foo/bar
    ///     </c>
    ///   </pre>
    /// </summary>
    /// <param name="pathString">The relative path from this reference.</param>
    /// <returns>
    ///   the child
    ///   <see cref="StorageReference" />
    ///   .
    /// </returns>
    public StorageReference Child(string pathString) {
      return new StorageReference(firebaseStorage, Internal.Child(pathString));
    }

    /// <summary>
    ///   Uploads byte data to this
    ///   <see cref="StorageReference" />
    ///   .
    ///   This is not recommended for large files. Instead upload a file via
    ///   <see>
    ///     <cref>PutFile</cref>
    ///   </see>
    ///   or a Stream via
    ///   <see>
    ///     <cref>PutStream</cref>
    ///   </see>
    ///   .
    /// </summary>
    /// <param name="bytes">The byte[] to upload.</param>
    /// <param name="customMetadata">
    ///   <see cref="MetadataChange" />
    ///   containing additional information (MIME type, etc.)
    ///   about the object being uploaded.
    /// </param>
    /// <param name="progressHandler">
    ///  usually an instance of <see cref="StorageProgress"/> that will
    ///  receive periodic updates during the operation. This value can
    ///  be null.</param>
    /// <param name="cancelToken">A CancellationToken to control the operation
    ///  and possibly later cancel it.  This value may be CancellationToken.None
    ///  to indicate no value.</param>
    /// <param name="previousSessionUri">A Uri previously obtained by
    ///  <see cref="UploadState.UploadSessionUri"/> that can be used to resume
    ///  a previously interrupted upload.</param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the upload.
    /// </returns>
    public Task<StorageMetadata> PutBytesAsync(
        byte[] bytes,
        MetadataChange customMetadata = null,
        IProgress<UploadState> progressHandler = null,
        CancellationToken cancelToken = default(CancellationToken),
        Uri previousSessionUri = null) {
      return PutBytesUsingCompletionSourceAsync(bytes, customMetadata, progressHandler, cancelToken,
                                                previousSessionUri,
                                                new TaskCompletionSource<StorageMetadata>());
    }

    /// <summary>
    /// Call Internal.PutBytesUsingMonitorControllerAsync while maintaining a reference to
    //  monitorController and metadata until the operation is complete.
    /// </summary>
    /// <param name="buffer">Address of pinned buffer to upload.</param>
    /// <param name="bufferSize">Size of buffer in bytes.</param>
    /// <param name="metadata">Metadata to upload or null to use defaults.</param>
    /// <param name="monitorController">Object that can be used to monitor and control the
    /// transfer.</param>
    /// <param name="cancellationToken">Token which can be used to cancel the upload.</param>
    /// <returns>Task that indicates when the upload is complete.
    /// NOTE: This task is the object constructed from firebase::Future.</returns>
    private Task<MetadataInternal> PutBytesUsingMonitorControllerAsync(
        IntPtr buffer, uint bufferSize, MetadataInternal metadata,
        MonitorControllerInternal monitorController, CancellationToken cancellationToken) {
      var task = Internal.PutBytesUsingMonitorControllerAsync(buffer, bufferSize, metadata,
                                                              monitorController);
      monitorController.RegisterCancellationToken(cancellationToken);
      return task.ContinueWith((completedTask) => {
          monitorController.Dispose();
          if (metadata != null) metadata.Dispose();
          return completedTask;
        }).Unwrap();
    }

    /// <summary>
    /// Uploads an array of bytes signally the specified completion source when complete.
    /// </summary>
    /// <param name="bytes">Array of bytes to upload.</param>
    /// <param name="customMetadata">Metadata to upload.</param>
    /// <param name="progressHandler">Handler to report progress to.</param>
    /// <param name="cancelToken">Token used to signal task cancellation.</param>
    /// <param name="previousSessionUri">URL used to resume upload.</param>
    /// <param name="completionSource">Completion source that is signalled when the operation is
    /// complete.</param>
    /// <returns>Task that indicates when the upload is complete.</returns>
    private Task<StorageMetadata> PutBytesUsingCompletionSourceAsync(
        byte[] bytes,
        MetadataChange customMetadata,
        IProgress<UploadState> progressHandler,
        CancellationToken cancelToken,
        Uri previousSessionUri,
        TaskCompletionSource<StorageMetadata> completionSource) {
      var uploadState = new UploadState(this, bytes.Length);
      var transferStateUpdater = new TransferStateUpdater<UploadState>(
        this, progressHandler, uploadState, uploadState.State);
      // TODO(smiles): Add support for resumable uploads when b/68320317 is resolved.
      var bytesHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      PutBytesUsingMonitorControllerAsync(
          bytesHandle.AddrOfPinnedObject(), (uint)bytes.Length,
          StorageMetadata.BuildMetadataInternal(customMetadata),
          transferStateUpdater.MonitorController, cancelToken)
        .ContinueWith(task => {
            bytesHandle.Free();
            CompleteTask(task, completionSource,
                         () => {
                           var metadata = new StorageMetadata(this, task.Result);
                           transferStateUpdater.SetMetadata(metadata);
                           return metadata;
                         }, "PutBytes");
          });
      return completionSource.Task;
    }

    /// <summary>
    /// Call Internal.PutFileUsingMonitorControllerAsync while maintaining a reference to
    //  monitorController and metadata until the operation is complete.
    /// </summary>
    /// <param name="path">Path (URI string) of the file to upload.</param>
    /// <param name="metadata">Metadata to upload or null to use defaults.</param>
    /// <param name="monitorController">Object that can be used to monitor and control the
    /// transfer.</param>
    /// <param name="cancellationToken">Token which can be used to cancel the upload.</param>
    /// <returns>Task that indicates when the upload is complete.
    /// NOTE: This task is the object constructed from firebase::Future.</returns>
    internal Task<MetadataInternal> PutFileUsingMonitorControllerAsync(
        string path, MetadataInternal metadata, MonitorControllerInternal monitorController,
        CancellationToken cancellationToken) {
      var task = Internal.PutFileUsingMonitorControllerAsync(path, metadata, monitorController);
      monitorController.RegisterCancellationToken(cancellationToken);
      return task.ContinueWith(completedTask => {
          monitorController.Dispose();
          if (metadata != null) metadata.Dispose();
          return completedTask;
        }).Unwrap();
    }

    /// <summary>
    ///   Uploads from a content URI to this
    ///   <see cref="StorageReference" />
    ///   .
    /// </summary>
    /// <param name="filePath">
    ///   The source of the upload.
    ///   This should be a file system URI representing the path the object
    ///   should be uploaded from.
    /// </param>
    /// <param name="customMetadata">
    ///   <see cref="MetadataChange" />
    ///   containing additional information (MIME
    ///   type, etc.) about the object being uploaded.
    /// </param>
    /// <param name="progressHandler">
    ///  usually an instance of <see cref="StorageProgress"/> that will
    ///  receive periodic updates during the operation. This value can
    ///  be null.</param>
    /// <param name="cancelToken">A CancellationToken to control the operation
    ///  and possibly later cancel it.  This value may be CancellationToken.None
    ///  to indicate no value.</param>
    /// <param name="previousSessionUri">A Uri previously obtained by
    ///  <see cref="UploadState.UploadSessionUri"/> that can be used to resume
    ///  a previously interrupted upload.</param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the upload.
    /// </returns>
    public Task<StorageMetadata> PutFileAsync(
        string filePath,
        MetadataChange customMetadata = null,
        IProgress<UploadState> progressHandler = null,
        CancellationToken cancelToken = default(CancellationToken),
        Uri previousSessionUri = null) {
      Preconditions.CheckArgument(filePath != null, "filePath cannot be null");
      var result = new TaskCompletionSource<StorageMetadata>();
      string filePathUriOrLocalPath = filePath.StartsWith("file://") ?
        (new Uri(filePath)).LocalPath : filePath;
      if (File.Exists(filePathUriOrLocalPath)) {
        var uploadState = new UploadState(this, (new FileInfo(filePathUriOrLocalPath)).Length);
        var transferStateUpdater = new TransferStateUpdater<UploadState>(
          this, progressHandler, uploadState, uploadState.State);
        PutFileUsingMonitorControllerAsync(
          filePath, StorageMetadata.BuildMetadataInternal(customMetadata),
          transferStateUpdater.MonitorController, cancelToken).ContinueWith(task => {
              CompleteTask(task, result,
                           () => {
                             var metadata = new StorageMetadata(this, task.Result);
                             transferStateUpdater.SetMetadata(metadata);
                             return metadata;
                           }, "PutFile");
            });
      } else {
        result.SetException(new FileNotFoundException(String.Format("{0} not found",
                                                                    filePathUriOrLocalPath),
                                                      filePath));
      }
      return result.Task;
    }

    /// <summary>
    ///   Uploads a stream of data to this
    ///   <see cref="StorageReference" />
    ///   .
    ///   The stream will remain open at the end of the upload.
    /// </summary>
    /// <param name="stream">
    ///   The
    ///   <see cref="Stream" />
    ///   to upload.
    /// </param>
    /// <param name="customMetadata">
    ///   <see cref="MetadataChange" />
    ///   containing additional information (MIME type, etc.)
    ///   about the object being uploaded.
    /// </param>
    /// <param name="progressHandler">
    ///  usually an instance of <see cref="StorageProgress"/> that will
    ///  receive periodic updates during the operation. This value can
    ///  be null.</param>
    /// <param name="cancelToken">A CancellationToken to control the operation
    ///  and possibly later cancel it.  This value may be CancellationToken.None
    ///  to indicate no value.</param>
    /// <param name="previousSessionUri">A Uri previously obtained by
    ///  <see cref="UploadState.UploadSessionUri"/> that can be used to resume
    ///  a previously interrupted upload.</param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the upload.
    /// </returns>
    public Task<StorageMetadata> PutStreamAsync(
        Stream stream,
        MetadataChange customMetadata = null,
        IProgress<UploadState> progressHandler = null,
        CancellationToken cancelToken = default(CancellationToken),
        Uri previousSessionUri = null) {
      Preconditions.CheckArgument(stream != null, "stream cannot be null");
      var result = new TaskCompletionSource<StorageMetadata>();
      // TODO(smiles): *STOP SHIP* This is awful as it copies the *entire* stream into memory then
      // copies it *again* into a byte array that can be passed to PutBytes().  b/68200113
      (new Thread(() => {
          byte[] buffer = new byte[512];
          using (MemoryStream memoryStream = new MemoryStream()) {
            while (true) {
              int read = stream.Read(buffer, 0, buffer.Length);
              if (read <= 0) break;
              memoryStream.Write(buffer, 0, read);
            }
            PutBytesUsingCompletionSourceAsync(memoryStream.ToArray(), customMetadata,
                                               progressHandler, cancelToken, previousSessionUri,
                                               result);
          }
        })).Start();
      return result.Task;
    }

    /// <summary>
    ///   Retrieves metadata associated with an object at this
    ///   <see cref="StorageReference" />
    ///   .
    /// </summary>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task<StorageMetadata> GetMetadataAsync() {
      var result = new TaskCompletionSource<StorageMetadata>();
      Internal.GetMetadataAsync().ContinueWith(task => {
          CompleteTask(task, result, () => { return new StorageMetadata(this, task.Result); },
                       "GetMetadata");
        });
      return result.Task;
    }

    /// <summary>
    ///   Retrieves a long lived download URL with a revokable token.
    /// </summary>
    /// <remarks>
    ///   Retrieves a long lived download URL with a revokable token.
    ///   This can be used to share the file with others, but can be revoked by a developer
    ///   in the Firebase Console if desired.
    /// </remarks>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task<Uri> GetDownloadUrlAsync() {
      var result = new TaskCompletionSource<Uri>();
      Internal.GetDownloadUrlAsync().ContinueWith(task => {
          CompleteTask(task, result,
                       () => {
                         var url = task.Result;
                         if (String.IsNullOrEmpty(url)) return null;
                         return FirebaseStorage.ConstructFormattedUri(url);
                       }, "GetDownloadUrl.");
        });
      return result.Task;
    }

    /// <summary>
    ///   Updates the metadata associated with this
    ///   <see cref="StorageReference" />
    ///   .
    /// </summary>
    /// <param name="metadata">
    ///   A
    ///   <see cref="MetadataChange" />
    ///   object with the metadata to update.
    /// </param>
    /// <returns>
    ///   a
    ///   <see cref="System.Threading.Tasks.Task" />
    ///   that will return the final
    ///   <see cref="StorageMetadata" />
    ///   once the operation
    ///   is complete.
    /// </returns>
    public Task<StorageMetadata> UpdateMetadataAsync(MetadataChange metadata) {
      Preconditions.CheckNotNull(metadata);
      var result = new TaskCompletionSource<StorageMetadata>();
      var metadataInternal = metadata.Build().Internal;
      Internal.UpdateMetadataAsync(metadataInternal).ContinueWith(
        task => {
          metadataInternal.Dispose();
          CompleteTask(task, result, () => { return new StorageMetadata(this, task.Result); },
                       "UpdateMetadata");
        });
      return result.Task;
    }

    /// <summary>
    ///   Downloads the object from this
    ///   <see cref="StorageReference" />
    ///   A byte array will be allocated large enough to hold the entire file in memory.
    ///   Therefore, using this method will impact memory usage of your process. If you are
    ///   downloading many large files,
    ///   <see>
    ///     <cref>GetStream(StreamDownloadTask.StreamProcessor)</cref>
    ///   </see>
    ///   may be a better option.
    /// </summary>
    /// <param name="maxDownloadSizeBytes">
    ///   The maximum allowed size in bytes that will be allocated.
    ///   Set this parameter to prevent out of memory conditions from
    ///   occurring. If the download exceeds this limit, the task will
    ///   fail and an
    ///   <see cref="System.IndexOutOfRangeException" />
    ///   will be returned.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task<byte[]> GetBytesAsync(long maxDownloadSizeBytes) {
      return GetBytesAsync(maxDownloadSizeBytes, progressHandler: null,
                           cancelToken: default(CancellationToken));
    }

    /// <summary>
    /// Call Internal.GetBytesUsingMonitorControllerAsync while maintaining a reference to
    /// monitorController until the operation is complete.
    /// </summary>
    /// <param name="buffer">Address of pinned buffer to read into.</param>
    /// <param name="bufferSize">Size of buffer in bytes.</param>
    /// <param name="monitorController">Object that can be used to monitor and control the
    /// transfer.</param>
    /// <param name="cancellationToken">Token which can be used to cancel the transfer.</param>
    /// <returns>Task that indicates when the download is complete.
    /// NOTE: This task is the object constructed from firebase::Future.</returns>
    internal Task<long> GetBytesUsingMonitorControllerAsync(
        System.IntPtr buffer, uint bufferSize, MonitorControllerInternal monitorController,
        CancellationToken cancellationToken) {
      var task = Internal.GetBytesUsingMonitorControllerAsync(buffer, bufferSize,
                                                              monitorController);
      monitorController.RegisterCancellationToken(cancellationToken);
      return task.ContinueWith(completedTask => {
          monitorController.Dispose();
          return completedTask;
        }).Unwrap();
    }

    /// <summary>
    ///   Downloads the object from this
    ///   <see cref="StorageReference" />
    ///   A byte array will be allocated large enough to hold the entire file in memory.
    ///   Therefore, using this method will impact memory usage of your process. If you are
    ///   downloading many large files,
    ///   <see>
    ///     <cref>GetStream(StreamDownloadTask.StreamProcessor)</cref>
    ///   </see>
    ///   may be a better option.
    /// </summary>
    /// <param name="maxDownloadSizeBytes">
    ///   The maximum allowed size in bytes that will be allocated.
    ///   Set this parameter to prevent out of memory conditions from
    ///   occurring. If the download exceeds this limit, the task will
    ///   fail and an
    ///   <see cref="System.IndexOutOfRangeException" />
    ///   will be returned.
    /// </param>
    /// <param name="progressHandler">
    ///  usually an instance of <see cref="StorageProgress"/> that will
    ///  receive periodic updates during the operation. This value can
    ///  be null.</param>
    /// <param name="cancelToken">A CancellationToken to control the operation
    ///  and possibly later cancel it.  This value may be CancellationToken.None
    ///  to indicate no value.</param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task<byte[]> GetBytesAsync(long maxDownloadSizeBytes,
                                      IProgress<DownloadState> progressHandler,
                                      CancellationToken cancelToken = default(CancellationToken)) {
      Logger.LogMessage(LogLevel.Debug,
                        String.Format("Get up to {0} bytes.", maxDownloadSizeBytes > 0 ?
                                      maxDownloadSizeBytes.ToString() : "all"));
      var result = new TaskCompletionSource<byte[]>();
      GetMetadataAsync().ContinueWith(metadataTask => {
          var successful = CompleteTask(
            metadataTask, result, () => {
              // If the returned metadata describes a zero sized file, abort.
              if (metadataTask.Result.SizeBytes <= 0) {
                return null;
              }
              // NOTE: This value is unused, we just return a non-null value here to indicate
              // success.
              return new byte[1];
            },
            "Get file size", setCompletionSourceResult: false);
          if (successful == null) return;
          var metadata = metadataTask.Result;
          var sizeBytes = metadata.SizeBytes;
          Logger.LogMessage(LogLevel.Debug, String.Format("Fetched metadata: {0}",
                                                          metadata.AsString()));
          Exception faultException = null;
          if (maxDownloadSizeBytes > 0 && sizeBytes > maxDownloadSizeBytes) {
            faultException = new IndexOutOfRangeException(
              String.Format("File size {0} is larger than the maximum download size {1}",
                            sizeBytes, maxDownloadSizeBytes));
          }

          // Clamp the download size to sizeBytes if maxDownloadSizeBytes is specified
          // (i.e not zero) and greater than the size of the file.
          var downloadSizeBytes = (maxDownloadSizeBytes > 0 && maxDownloadSizeBytes < sizeBytes) ?
            maxDownloadSizeBytes : sizeBytes;
          Logger.LogMessage(LogLevel.Debug, String.Format("Downloading {0} bytes",
                                                          downloadSizeBytes));
          var downloadState = new DownloadState(this, downloadSizeBytes);
          var transferStateUpdater = new TransferStateUpdater<DownloadState>(
            this, progressHandler, downloadState, downloadState.State);
          var bytes = new byte[downloadSizeBytes];
          var bytesHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
          GetBytesUsingMonitorControllerAsync(
            bytesHandle.AddrOfPinnedObject(), (uint)bytes.Length,
            transferStateUpdater.MonitorController, cancelToken).ContinueWith(downloadTask => {
                bytesHandle.Free();
                CompleteTask(downloadTask, result, () => {
                    if (faultException != null) {
                      result.SetException(faultException);
                    }
                    return bytes;
                  }, "GetBytes");
              });
        });
      return result.Task;
    }

    /// <summary>
    /// Call Internal.GetFileUsingMonitorControllerAsync while maintaining a reference to
    /// monitorController until the operation is complete.
    /// </summary>
    /// <param name="path">Path (URI string) of the file to read to.</param>
    /// <param name="monitorController">Object that can be used to monitor and control the
    /// transfer.</param>
    /// <param name="cancellationToken">Token which can be used to cancel the transfer.</param>
    /// <returns>Task that indicates when the download is complete.
    /// NOTE: This task is the object constructed from firebase::Future.</returns>
    private Task<long> GetFileUsingMonitorControllerAsync(
        string path, MonitorControllerInternal monitorController,
        CancellationToken cancellationToken) {
      var task = Internal.GetFileUsingMonitorControllerAsync(path, monitorController);
      monitorController.RegisterCancellationToken(cancellationToken);
      return task.ContinueWith(completedTask => {
          monitorController.Dispose();
          return completedTask;
        }).Unwrap();
    }

    /// <summary>
    ///   Downloads the object at this
    ///   <see cref="StorageReference" />
    ///   to a specified system
    ///   filepath.
    /// </summary>
    /// <param name="destinationFilePath">
    ///   A file system URI representing the path the object should be
    ///   downloaded to.
    /// </param>
    /// <param name="progressHandler">
    ///  usually an instance of <see cref="StorageProgress"/> that will
    ///  receive periodic updates during the operation. This value can
    ///  be null.</param>
    /// <param name="cancelToken">A CancellationToken to control the operation
    ///  and possibly later cancel it.  This value may be CancellationToken.None
    ///  to indicate no value.</param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task GetFileAsync(string destinationFilePath,
                             IProgress<DownloadState> progressHandler = null,
                             CancellationToken cancelToken = default(CancellationToken)) {
      Logger.LogMessage(LogLevel.Debug, String.Format("Downloading to {0}.",
                                                      destinationFilePath));
      var downloadState = new DownloadState(this, -1);
      var transferStateUpdater = new TransferStateUpdater<DownloadState>(
        this, progressHandler, downloadState, downloadState.State);
      return GetFileUsingMonitorControllerAsync(
        destinationFilePath, transferStateUpdater.MonitorController,
        cancelToken).ContinueWith((task) => {
            return (new TaskCompletionStatus(task, "GetFile", Logger)).ToTask();
          }).Unwrap();
    }

    /// <summary>
    ///   Downloads the object at this
    ///   <see cref="StorageReference" />
    ///   via a
    ///   <see cref="Stream" />
    ///   .
    ///   The resulting InputStream should be not be accessed on the main thread
    ///   because calling into it may block the calling thread.
    /// </summary>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation.
    /// </returns>
    public Task<Stream> GetStreamAsync() {
      var result = new TaskCompletionSource<Stream>();
      // TODO(smiles): Fix this interface as it's totally odd.  Reading an entire file into a stream
      // seems broken rather than simply providing a stream with a small-ish buffer that blocks
      // downloading when it's not being read and is complete when it is closed.
      // Also, this doesn't support progress reporting or cancellation.
      GetBytesAsync(0).ContinueWith(task => {
          CompleteTask(task, result, () => {
                         var bytes = task.Result;
                         return bytes != null ? new MemoryStream(bytes) : null;
                       },
                       "GetStreamAsync");
        });
      return result.Task;
    }

    /// <summary>
    ///   Downloads the object at this
    ///   <see cref="StorageReference" />
    ///   via a
    ///   <see cref="Stream" />
    ///   .
    /// </summary>
    /// <param name="streamProcessor">
    ///   A delegate
    ///   that is responsible for
    ///   reading data from the
    ///   <see cref="Stream" />
    ///   .
    ///   The delegate
    ///   is called on a background
    ///   thread and exceptions thrown from this object will be returned as
    ///   a failure to the Task
    /// </param>
    /// <param name="progressHandler">
    ///  usually an instance of <see cref="StorageProgress"/> that will
    ///  receive periodic updates during the operation. This value can
    ///  be null.</param>
    /// <param name="cancelToken">A CancellationToken to control the operation
    ///  and possibly later cancel it.  This value may be CancellationToken.None
    ///  to indicate no value.</param>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task GetStreamAsync(Action<Stream> streamProcessor,
                               IProgress<DownloadState> progressHandler = null,
                               CancellationToken cancelToken = default(CancellationToken)) {
      // TODO(smiles): *STOP SHIP* Fix streaming support. b/68200113
      return GetBytesAsync(0, progressHandler: progressHandler,
                           cancelToken: cancelToken).ContinueWith(task => {
                               if (!task.IsFaulted && !task.IsCanceled) {
                                 var bytes = task.Result;
                                 if (bytes != null) {
                                   streamProcessor(new MemoryStream(bytes));
                                 }
                               }
                               return task;
                             }).Unwrap();
    }

    /// <summary>
    ///   Deletes the object at this
    ///   <see cref="StorageReference" />
    ///   .
    /// </summary>
    /// <returns>
    ///   A <see cref="Task"/>
    ///   which can be used to monitor the operation and obtain the result.
    /// </returns>
    public Task DeleteAsync() {
      return Internal.DeleteAsync().ContinueWith((task) => {
          if (task.IsFaulted && (task.Exception != null)) {
            return Task.Run(() => { throw StorageException.CreateFromException(task.Exception); });
          }
          return task;
        }).Unwrap();
    }

    /// <returns>
    ///   This object in URI form, which can then be shared and passed into
    ///   <see cref="FirebaseStorage.GetReferenceFromUrl" />
    ///   .
    /// </returns>
    public override string ToString() {
      return String.Format("gs://{0}{1}", Bucket, Path);
    }

    /// <summary>
    /// Compares two storage reference URIs.
    /// </summary>
    /// <returns>true if two references point to the same path, false otherwise.</returns>
    public override bool Equals(object other) {
      if (!(other is StorageReference)) {
        return false;
      }
      var otherStorage = (StorageReference) other;
      return otherStorage.ToString().Equals(ToString());
    }

    /// <summary>
    /// Create a hash of the URI string used by this reference.
    /// </summary>
    /// <returns>Hash of this reference's URI.</returns>
    public override int GetHashCode() {
      return ToString().GetHashCode();
    }

    // C# proxy for the firebase::storage::StorageReference object.
    internal StorageReferenceInternal Internal { get; private set; }

    /// <summary>
    /// Complete the specified task by either setting a result on the completion source or
    /// forwarding the failures status from the completion source.
    /// </summary>
    /// <param name="task">Task to read completion status from.</param>
    /// <param name="completionSource">Completion source to set  the result on.</param>
    /// <param name="getResult">Function that is called if the task successfully completed.  This
    /// function should return the result if it succeeded or null if it failed.</param>
    /// <param name="operationDescription">Description of the operation being completed.  This is
    /// used to log the completion and to form the exception string when setting an
    /// InvalidOperationException on the task, if the task failed but didn't contain an exception.
    /// </param>
    /// <param name="setCompletionSourceResult">Whether to set the result on the task completion
    /// source.  This can be disabled if the completion of the completionSource should occur
    /// outside of this method.</param>
    /// <param name="operationDescription">Description of the operation being completed used when
    /// logging is enabled.</param>
    /// <returns>Value returned by setResult() if successful, null otherwise.</returns>
    private O CompleteTask<I, O>(Task<I> task, TaskCompletionSource<O> completionSource,
                                 Func<O> getResult, string operationDescription,
                                 bool setCompletionSourceResult = true) {
      O result = default(O);
      var status = new TaskCompletionStatus(task, operationDescription, Logger);
      if (status.IsSuccessful) {
        result = getResult();
        if (result != null) {
          if (setCompletionSourceResult) completionSource.SetResult(result);
          return result;
        }
      }
      if (status.IsCanceled) {
        completionSource.SetCanceled();
        return result;
      }
      completionSource.SetException(status.Exception);
      return result;
    }
  }
}