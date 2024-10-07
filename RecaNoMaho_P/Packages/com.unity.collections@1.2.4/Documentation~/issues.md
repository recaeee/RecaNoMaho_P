---
uid: collections-known-issues
---

# Known issues

All containers allocated with `Allocator.Temp` on the same thread use a shared `AtomicSafetyHandle` instance rather than each having their own. On the one hand, this is fine because Temp allocated collections cannot be passed into jobs. On the other hand, this is problematic when using `NativeHashMap`, `NativeMultiHashMap`, `NativeHashSet`, and `NativeList` together in situations where their secondary safety handle is used. (A secondary safety handle ensures that a NativeArray which aliases a NativeList gets invalidated when the NativeList is reallocated due to resizing.)

Operations that invalidate an enumerator for these collection types (or invalidate the `NativeArray` returned by `NativeList.AsArray`) will also invalidate all other previously acquired enumerators. For example, this will throw when safety checks are enabled:

```c#
var list = new NativeList<int>(Allocator.Temp);
list.Add(1);

// This array uses the secondary safety handle of the list, which is
// shared between all Allocator.Temp allocations.
var array = list.AsArray();

var list2 = new NativeHashSet<int>(Allocator.Temp);

// This invalidates the secondary safety handle, which is also used
// by the list above.
list2.TryAdd(1);

// This throws an InvalidOperationException because the shared safety
// handle was invalidated.
var x = array[0];
```

This defect will be addressed in a future release.
