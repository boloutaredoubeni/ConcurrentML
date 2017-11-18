namespace ConcurrentML.Core

open System.Threading.Channels

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

        // static member Choose<'TResult>(asyncComputations: seq<Async<'TResult>>): Async<'TResult> = http://fssnip.net/dN
        //  wrap around chooseTask

        // static member ChooseTask<'TResult>(asyncComputations: seq<Task<'TResult>>): Async<'TResult> =
        //  use Task.WhenAny

        // static member Select<'TResult>(asyncComputations: seq<Async<'TResult>>): Async<'TResult> =
        //     (Async.RunSynchronously << Async.Choose) asyncComputations

        member this.Map (continuation: 'T -> 'U) =
            async {
                let! result = this
                return continuation result
            }


    let select tasks = ()

    let wrap event handler = ()