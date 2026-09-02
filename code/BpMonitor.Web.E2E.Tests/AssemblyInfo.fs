module internal AssemblyInfo

open Xunit
open BpMonitor.Web.E2E

// Default -maxThreads (1/CPU) lets CI's 4-vCPU runner boot cold apps at once vs 16 locally — pin it so local runs reproduce CI's contention.
[<assembly: CollectionBehavior(MaxParallelThreads = 2)>]
// One BpMonitor.Web process + one browser per engine for the whole assembly, not one per class.
[<assembly: AssemblyFixture(typeof<AppFixture>)>]
do ()
