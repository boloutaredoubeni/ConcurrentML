namespace ConcurrentML.Core

open System.Threading.Channels

module CML = 

    module Channel =

        type Chan<'T> = private Channel of Channel<'T>
            
        let channel<'T> () = Channel (Channel.CreateUnbuffered<'T> ())

        let sendAsync (Channel chan) payload = 
            async {
                do! chan.Writer.WriteAsync (payload) |> Async.AwaitTask
            }

        let receiveAsync (Channel chan) = 
            async {
                return! chan.Reader.ReadAsync ()
                    |> (fun valueTask -> valueTask.AsTask())
                    |> Async.AwaitTask
            }

        let waitToReadAsync (Channel chan) =
            async {
                return! chan.Reader.WaitToReadAsync() |> Async.AwaitTask
            }

        let isDoneReading chan = (Async.RunSynchronously << waitToReadAsync) chan

        type Chan<'T> with
            member chan.SendAsync (payload) = sendAsync chan payload
            member chan.ReceiveAsync () = receiveAsync chan
            member chan.WaitToReadAsync () = waitToReadAsync chan
            member chan.IsDoneReading () = isDoneReading chan

    
    type Async<'T> with
        static member Loop<'TState> (stateFn: 'TState -> Async<'TState>, initialState: 'TState) =
            async {
                let rec loop (state: 'TState) = 
                    async {
                        let! nextState =  stateFn state
                        do! loop nextState
                    }
                do! loop initialState
            }
            |> Async.Start

    let select tasks = ()
    let wrap event handler = ()

    let forever<'TState> stateFn initialState = Async.Loop<'TState> (stateFn, initialState)
