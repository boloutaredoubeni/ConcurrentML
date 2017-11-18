// Learn more about F# at http://fsharp.org


open ConcurrentML.Core
open CML
open Channel

module Cell =

    type private Request<'T> = Read | Write of 'T

    type Cell<'T> private () =
        let requestChannel = channel<Request<'T>> ()
        let replyChannel = channel<'T> ()
        member __.GetAsync () =
            async {
                do! requestChannel.SendAsync (Read)
                return! replyChannel.ReceiveAsync()
            }
        member __.PutAsync (payload) =
            requestChannel.SendAsync (Write payload)
        member __.RunAsync (initialState) =
            let rec loop state =
                async {
                    let! incoming = requestChannel.ReceiveAsync ()
                    match incoming with
                    | Read -> 
                        do! replyChannel.SendAsync state
                        do! loop state
                    | Write state' -> do! loop state'
                }
            loop initialState

        static member StartServer<'T> (initialState: 'T) =
            let cell = Cell()
            do Async.Start (cell.RunAsync (initialState))
            cell

module PrimeSieve = 

    let private counter initialValue =
            let ch = channel ()
            let rec count i =
                async { 
                    do! sendAsync ch i
                    return! count (i + 1)
                }
            do Async.Start (count initialValue)
            ch

    let private filter prime reader =
            let writer = channel ()
            let rec loop () =
                async {
                    let! i = receiveAsync reader
                    if (i % prime) <> 0 
                        then do! sendAsync writer i
                    do! loop ()
                }
            Async.Start (loop ())
            writer

    let private sieve () =
        let primes = channel()
        let rec head stream = 
            async {
                let! p = receiveAsync stream
                do! sendAsync primes p
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
                        let y = (Async.RunSynchronously << seive'.ReceiveAsync) ()
                        let ys = Seq.toList xs 
                        let ys = y :: ys |> Seq.ofList
                        yield! loop (i - 1)  ys
                }
            return loop n Seq.empty
        }

module FibonacciSeries =
    let private add addendChannel augendChannel writerChannel =
        let addToWriter () =
            async {
                let! a = receiveAsync addendChannel
                let! b = receiveAsync augendChannel
                do! sendAsync writerChannel (a + b)
            }
        forever addToWriter ()

    let private delay initialState reader writer =
        let transfer state =
            async {
                match state with
                | None -> 
                    let! payload = receiveAsync reader
                    return Some payload
                | Some x -> 
                    do! sendAsync writer x
                    return None
            }
        forever transfer initialState
                
    let private copy reader listeners =
        let publish () =
            async {
                let! payload = receiveAsync reader
                do Seq.iter (fun channel -> 
                    Async.Start (sendAsync channel payload)) listeners
            }
        forever publish ()

    let fibonacciNetwork () =
        do printfn "Start Fibber Network"
        let writer = channel ()
        async {
            let ([c1; c2; c3; c4; c5]) = [
                channel () 
                channel ()
                channel () 
                channel ()
                channel () 
            ]
            do 
                delay (Some (bigint 0L)) c4 c5
                copy c2 [ c3; c4 ]
                add c3 c5 c1
                copy c1 [c2;  writer]
            do! c1.SendAsync (bigint 1L)
        }
        |> Async.Start
        writer



open Cell
open PrimeSieve
open FibonacciSeries

let RunCellProgram () = 
    async {
        let init = Some 0
        let cell = Cell.StartServer (init)
        let! x = cell.GetAsync() 
        printfn "Got %A, started with %A" x init
        let x = Some 1
        do! cell.PutAsync (x)
        printfn "Put %A, started with %A" x init 
        let! x = cell.GetAsync()
        printfn "Got %A, started with %A" x init
    }


let RunPrimeSieveAsyncProgram numberOfPrimes =
    async {
        let! primes' = primes numberOfPrimes
        return Seq.iteri (fun index prime -> do printfn "%d.\tPrime %d" index prime) primes'
    }

let RunFibonacciProgram numberOfFibs =
    let rec loop network counter =
        async {
            if numberOfFibs <= counter
                then return ()
                else 
                    let! fib = receiveAsync network
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
