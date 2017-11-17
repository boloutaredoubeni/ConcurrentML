// Learn more about F# at http://fsharp.org


open ConcurrentML.Core
open CML
open Channel

module Cell =

    type private Request<'T> = Read | Write of 'T

    type Cell<'T>() = 

        let requestChannel = channel<Request<'T>> ()
        let replyChannel = channel<'T> ()

        member __.RunAsync (init: 'T): unit =
            let rec loop (x: 'T) =
                match receive requestChannel with
                | Read -> 
                    send replyChannel x
                    loop x
                | Write x' -> loop x'
            loop init

        member __.Get (): 'T = 
            do send requestChannel Read
            receive replyChannel


        member __.Put (x: 'T): unit =  send requestChannel (Write x)

    let cell x =
        let c = Cell()
        do 
            spawn (fun () -> c.RunAsync (x))
            printfn "Running insync"
        c

module PrimeSieve = 

    let private counter initialValue =
        let ch = channel ()
        let rec count i =
            do 
                send ch i
                count (i + 1L)
        spawn (fun () -> count initialValue)
        ch

    let private filter prime reader =
        let writer = channel ()
        let rec loop () =
            let i = receive reader
            if (i % prime) <> 0L 
                then do send writer i
            do loop ()
        do spawn loop
        writer

    let private sieve () =
        let primes = channel()
        let rec head stream = do
            let p = receive stream
            do 
                send primes p
                printfn "Send %d to primes channel" p 
            (head (filter p stream))
        do spawn (fun () -> (head (counter 2L)))
        primes

    let primes n =
        let ch = sieve ()
        let rec loop i xs =
            seq {
                match (i, xs) with
                | 0L, xs -> yield! Seq.rev xs
                | i, xs -> 
                    let y = receive ch
                    let ys = Seq.toList xs 
                    let ys = y :: ys |> Seq.ofList
                    yield! loop (i - 1L)  ys
            }
        loop n Seq.empty   

module FibonacciSeries =
    let private add addendChannel augendChannel writerChannel =
        let addToWriter () =
            let a = Channel.receive addendChannel
            let b = Channel.receive augendChannel
            do Channel.send writerChannel (a + b)
        forever addToWriter ()

    let private delay initialState reader writer =
        let transfer = function
            | None -> Some (Channel.receive reader)
            | Some x -> 
                do send writer x
                None
        forever transfer initialState
                
    let private copy reader listeners =
        let publish () =
            let payload = Channel.receive reader
            do Seq.iter (fun channel -> Channel.send channel payload) listeners
        forever publish ()

    // let fibonacciNetwork () =
    //     let writer = channel ()
    //     let c1 =

open Cell
open PrimeSieve
   
[<EntryPoint>]
let main argv =
    do printfn "Hello World from F#!"
    let cell' = cell (Some 0)
    let (Some _) = cell'.Get()
    do cell'.Put (Some 1)
    let (Some x) = cell'.Get()
    do 
        printfn "%d" x
        printfn "%A" cell'
        primes 100L |> ignore
    0 // return an integer exit code
