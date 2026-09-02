module internal AssemblyInfo

open Xunit

// Default -maxThreads (1/CPU) lets CI's 4-vCPU runner boot 4 cold apps at once vs 16 locally — pin it so local runs reproduce CI's contention.
[<assembly: CollectionBehavior(MaxParallelThreads = 2)>]
do ()
