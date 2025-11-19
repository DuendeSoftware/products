// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Internal;

internal delegate bool ValidationRule<in T>(T value, out string message);
