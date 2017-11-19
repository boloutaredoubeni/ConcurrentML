namespace ConcurrentML.Core

open System.Threading.Channels
open System.Threading.Tasks

module CML =

    module Channel =

        /// Wrapper for a .NET Channel
        type Chan<'T> = private Channel of Channel<'T>

        ///**Description**
        /// Channel constructor
        ///**Parameters**
        ///
        ///
        ///**Output Type**
        ///  * `Chan<'T>`
        ///
        ///**Exceptions**
        ///
        let Channel<'T> () = Channel (Channel.CreateUnbuffered<'T> ())

        let private (|Writer|_|) (Channel chan) = Option.ofObj chan.Writer

        let private (|Reader|_|) (Channel chan) = Option.ofObj chan.Reader

        type Chan<'T> with
            /// Send a value asynchronously
            member chan.SendAsync (payload) =
                match chan with
                | Writer writer -> writer.WriteAsync (payload) |> Async.AwaitTask
                | _ -> failwith "ChannelWriter is missing"

            /// Receive a value asynchronously
            member chan.ReadAsync () =
                match chan with
                | Reader reader -> 
                    reader.ReadAsync ()
                    |> (fun valueTask -> valueTask.AsTask())
                    |> Async.AwaitTask
                | _ -> failwith "ChannelReader is missing"

            member chan.SendSynchronously = chan.SendAsync >> Async.RunSynchronously

            /// Receive a value asynchronously
            member chan.ReadSynchronously = chan.ReadAsync >> Async.RunSynchronously

    type Async<'T> with

        /// An Async service that runs the stateFn on every loop
        static member StartService (stateFn, initialState) =
            async {
                let rec loop (state: 'TState) =
                    let nextState = stateFn state
                    do loop nextState
                return loop initialState
            }

        static member StartService stateFn = Async.StartService (stateFn, ())
        
        static member Wrap (asyncEvent: Async<_>, continuation) =
            async {
                try
                    let! result = asyncEvent
                    return (Some << continuation) result
                with
                | _ -> return None
            }

        static member Select<'T> (asyncComputations: seq<Async<'T option>>) = 
            (Async.RunSynchronously << Async.Choice) asyncComputations

    module Async =
        let map continuation asyncEvent =
            async { 
                let! result = asyncEvent 
                return continuation result 
            }
