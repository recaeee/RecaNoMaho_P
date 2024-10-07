---
uid: collections-overview
---
# Unity Collections package

This package provides unmanaged data structures that can be used in jobs and Burst-compiled code.

The collections provided by this package fall into three categories:

- The collection types in `Unity.Collections` whose names start with `Native-` have safety checks for ensuring that they're properly disposed and are used in a thread-safe manner. 
- The collection types in `Unity.Collections.LowLevel.Unsafe` whose names start with `Unsafe-` do not have these safety checks.
- The remaining collection types are not allocated and contain no pointers, so effectively their disposal and thread safety are never a concern. These types hold only small amounts of data.

The `Native-` types perform safety checks to ensure that indexes passed to their methods are in bounds, but the other types in most cases do not.

Several `Native-` types have `Unsafe-` equivalents, for example, `NativeList` has `UnsafeList`, and `NativeHashMap` has `UnsafeHashMap`.

While you should generally prefer using the `Native-` collections over their `Unsafe-` equivalents, `Native-` collections cannot contain other `Native-` collections (owing to the implementation of their safety checks). So if, say, you want a list of lists, you can have a `NativeList<UnsafeList<T>>` or an `UnsafeList<UnsafeList<T>>`, but you cannot have a `NativeList<NativeList<T>>`. 

When safety checks are disabled, there is generally no significant performance difference between a `Native-` type and its `Unsafe-` equivalent. In fact, most `Native-` collections are implemented simply as wrappers of their `Unsafe-` counterparts. For example, `NativeList` is comprised of an `UnsafeList` plus a few handles used by the safety checks. 

For more information on the specific collection types, see the documentation on [Collection types](collection-types.md)
