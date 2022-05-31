// Copyright 2022 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
//using Firebase.Firestore.Internal;

namespace Firebase.Firestore {

/// <summary>
/// Options to customize transaction behavior for 
/// <see cref="FirebaseFirestore.RunTransactionAsync"/>.
/// </summary>
public sealed class TransactionOptions {

  // The lock that must be held during read and write operations to all instance variables.
  private readonly ReaderWriterLock _lock = new ReaderWriterLock();

  // The underlying C++ TransactionOptions object.
  private TransactionOptionsProxy _proxy = new TransactionOptionsProxy();

  /// <summary>
  /// Creates the default <c>TransactionOptions</c>.
  /// </summary>
  public TransactionOptions() {
  }

  /// <summary>
  /// The maximum number of attempts to commit, after which the transaction fails.
  /// </summary>
  ///
  /// <remarks>
  /// The default value is 5.
  /// Must be greater than zero.
  /// </remarks>
  public Int32 MaxAttempts {
    get {
      return _proxy.max_attempts();
    }
    set {
      _proxy.set_max_attempts(value);
    }
  }

  /// <inheritdoc />
  public override string ToString() {
    return nameof(TransactionOptions) + "{" + nameof(MaxAttempts) + "=" + MaxAttempts + "}";
  }

}

}
