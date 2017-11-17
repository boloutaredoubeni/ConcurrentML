namespace ConcurrentML.Core

open System.Threading.Channels

module CML = 

    module Channel =

        type Chan<'T> = private Channel of Channel<'T>
            
        type 'a chan = Chan<'a>

        let channel<'T> () = Channel (Channel.CreateUnbuffered<'T> ())

        let send (Channel chan) payload = 
            chan.Writer.WriteAsync (payload)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        let receive (Channel chan) = 
            (chan.Reader.ReadAsync ())
            |> (fun valueTask -> valueTask.AsTask())
            |> Async.AwaitTask
            |> Async.RunSynchronously
            
    let spawn f =
        async {
            return f()
        } |> Async.Start

    let forever f initialState =
        let rec loop state = loop (f state)
        do spawn (fun () -> loop initialState)


    
  

    



