// Learn more about F# at http://fsharp.org


open ConcurrentML.Core
open CML
open Channel

open System
open System.Drawing

type Console = Colorful.Console

module Cell =

    type Cell<'T> private () =
        let putChannel = Channel<'T> ()
        let getChannel = Channel<'T> ()
        member __.Get = getChannel.ReadAsync
        member __.Put = putChannel.SendAsync

        static member StartServer<'T> (initialState: 'T) =
            let cell = Cell()
            do Async.Start (cell.RunAsync (initialState))
            cell
        member __.RunAsync (initialState) =
            let rec loop state =
                Async.Select [
                    Async.Wrap (getChannel.SendAsync state,
                        fun _ -> loop state)
                    Async.Wrap (putChannel.ReadAsync (), loop)
                ]
                |> Option.map (function 
                    | Some state -> state
                    | None -> state)
            async {
                return (ignore << loop) initialState
            }


module PrimeSieve = 

    let private counter initialValue =
        let ch = Channel ()
        let rec count i =
            async { 
                do! ch.SendAsync i
                return! count (i + 1)
            }
        do Async.Start (count initialValue)
        ch

    let private filter prime (reader: Chan<_>) =
        let writer = Channel ()
        let rec loop () =
            async {
                let! i = reader.ReadAsync ()
                if (i % prime) <> 0 
                    then do! writer.SendAsync i
                do! loop ()
            }
        Async.Start (loop ())
        writer

    let private sieve () =
        let primes = Channel()
        let rec head (stream: Chan<_>) = 
            async {
                let! p = stream.ReadAsync ()
                do! primes.SendAsync p
                let filteredStream = filter p stream
                return! head filteredStream
            }
        Async.Start (head (counter 2))
        primes

    let primes n =
        async {
            let seive' = sieve ()
            let rec loop i xs =
                seq {
                    match (i, xs) with
                    | 0, xs -> yield! Seq.rev xs
                    | i, xs -> 
                        let y = (Async.RunSynchronously << seive'.ReadAsync) ()
                        let ys = Seq.toList xs 
                        let ys = y :: ys |> Seq.ofList
                        yield! loop (i - 1) ys
                }
            return loop n Seq.empty
        }

module FibonacciSeries =
    let private add (addendChannel: Chan<_>) (augendChannel: Chan<_>) (writerChannel: Chan<_>) =
        let addToWriter () =
            async {
                let decision =  
                    Async.Select [
                        Async.Wrap (addendChannel.ReadAsync (), 
                            fun a -> 
                                do Console.WriteLine (sprintf "ADD read addend %A, send augend %A" a a, Color.LawnGreen)
                                (a, augendChannel.ReadSynchronously ()))
                        Async.Wrap (augendChannel.ReadAsync (), 
                            fun b ->
                                do Console.WriteLine (sprintf "ADD read augend %A, send addend %A" b b, Color.DarkOliveGreen)
                                (addendChannel.ReadSynchronously (), b))
                    ]
                match decision with
                | Some (a, b) -> 
                    do Console.WriteLine (sprintf "ADD send writer %A" (a + b), Color.Chartreuse)
                    return writerChannel.SendSynchronously (a + b)
                | _ ->
                    do
                        let message = "ADD Unable to make a choice for add network"
                        Console.WriteLine (message, Color.ForestGreen)
                        failwith message
            }
            |> Async.Ignore
            |> Async.RunSynchronously
        do Console.WriteLine ("Start Add Network", Color.SpringGreen)
        Async.StartService addToWriter

    let private delay initialState (reader: Chan<_>) (writer: Chan<_>) =
        let transfer state =
            async {
                match state with
                | None -> 
                    let payload = reader.ReadSynchronously ()
                    do Console.WriteLine ((sprintf "DELAY read reader %A" payload), Color.Navy)
                    return Some payload
                | Some x -> 
                    do 
                        Console.WriteLine (sprintf "DELAY send writer %A" x, Color.SkyBlue)
                        writer.SendSynchronously x
                    return None
            }
            |> Async.RunSynchronously
        do Console.WriteLine ("Start Delay Network", Color.Aqua)
        Async.StartService (transfer, initialState)
                
    let private copy (reader: Chan<_>) (writer1: Chan<_>) (writer2: Chan<_>) =
        let publish () =
            async {
                let! payload = reader.ReadAsync ()
                let decision = 
                    Async.Select [
                        Async.Wrap (writer1.SendAsync payload,
                            fun _ ->
                                do Console.WriteLine (sprintf "COPY send writer1 %A" payload, Color.LemonChiffon)
                                writer2.SendSynchronously payload)
                        Async.Wrap (writer2.SendAsync payload,
                            fun _ -> 
                                do Console.WriteLine (sprintf "COPY send writer2 %A" payload, Color.Goldenrod)
                                writer1.SendSynchronously payload)
                    ]
                match decision with
                | Some () -> return ()
                | _ -> 
                    do
                        let message = "Unable to make a choice for copy network"
                        Console.WriteLine (message, Color.PaleGoldenrod)
                        failwith message
            }
            |> Async.RunSynchronously
        do Console.WriteLine ("Start Copy Network", Color.Khaki)
        Async.StartService publish

    let fibonacciNetwork () =
        do printfn "Start Fibber Network"
        let writer = Channel ()
        async {
            let ([c1; c2; c3; c4; c5]) = [
                Channel () 
                Channel ()
                Channel () 
                Channel ()
                Channel () 
            ]
            [   delay (Some (bigint 0L)) c4 c5
                copy c2 c3 c4
                add c3 c5 c1
                copy c1 c2 writer
                c1.SendAsync (bigint 1L) ]
            |> Seq.iter (Async.Start)
        }
        |> Async.Start
        writer



open Cell
open PrimeSieve
open FibonacciSeries

let RunCellProgram () = 
    async {
        let init = Some 0 // 0
        let cell = Cell.StartServer (init)
        let! x = cell.Get()  // 0
        printfn "Got %A, started with %A" x init
        let x = Some 1
        do! cell.Put (x) // 1
        printfn "Put %A, started with %A" x init 
        let! x = cell.Get() // 1
        printfn "Got %A, started with %A" x init
        let x = None // none
        do! cell.Put (x) //none
        printfn "Put %A, started with %A" x init
        let! x = cell.Get() // none
        printfn "Got %A, started with %A" x init
    }


let RunPrimeSieveAsyncProgram numberOfPrimes =
    async {
        let! primes' = primes numberOfPrimes
        return Seq.iteri (fun index prime -> do printfn "%d.\tPrime %d" index prime) primes'
    }

// FIXME: adding Async.Choice to the fib network introduced a deadlock
let RunFibonacciProgram numberOfFibs =
    let rec loop (network: Chan<_>) counter =
        async {
            if numberOfFibs <= counter
                then return ()
                else 
                    let! fib = network.ReadAsync ()
                    do printfn "%d.\tFib %A" counter fib
                    do! loop network (counter + 1)
        }
    loop (fibonacciNetwork()) 0 

let [<Literal>] NumberOfFibs = 100
let [<Literal>] NumberOfPrimes = 100
[<EntryPoint>]
let main argv =
    do printfn "Hello World from F#!"
    Async.Parallel [
        RunCellProgram ()
        RunPrimeSieveAsyncProgram NumberOfPrimes
        RunFibonacciProgram NumberOfFibs
    ]
    |> Async.RunSynchronously
    |> ignore
    0 // return an integer exit code
