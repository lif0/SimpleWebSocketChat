﻿namespace Chat.Api.Controllers

open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open System
open System.Collections.Concurrent
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open System.Net.WebSockets
open System.Collections.Concurrent
open Chat

[<ApiController>]
[<Route("ws/")>]
type ChannelController()=
  inherit ControllerBase()

  let recieve(ws:WebSocket) (f:WebSocketReceiveResult*byte array->Async<unit>)= async {
    let buffer = Array.zeroCreate (1024*4)
    while (ws.State = WebSocketState.Open) do
      let! result = ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)|> Async.AwaitTask
      do! f(result, buffer)
  }


  [<HttpGet>]
  [<Route("{channelId}")>]
  member this.Connect(channelId:string)= async {
    if this.HttpContext.WebSockets.IsWebSocketRequest then
      if Global.channels.ContainsKey channelId then
        let roomId = Global.channels.Item channelId
        let sh = Global.rooms.Item roomId
        let! ws = this.HttpContext.WebSockets.AcceptWebSocketAsync()|> Async.AwaitTask
        let nick = (Guid.NewGuid().ToString())
        do! sh.OnConnected nick ws

        let f(result:WebSocketReceiveResult, buffer:byte array) = async {
          match result.MessageType with
          | WebSocketMessageType.Text -> do! sh.Recieve nick result buffer
          | WebSocketMessageType.Close -> 
              do! sh.OnDisconnet nick 
              if sh.IsEmpty() then
                Global.rooms.TryRemove roomId |> ignore
                Global.channels.TryRemove channelId |> ignore 
          |_ -> ()
        }          

        do! recieve ws f
  }