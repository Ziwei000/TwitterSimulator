open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open System
open System.Net
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open System.Collections.Generic
open Akka
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Diagnostics

let system = System.create "system" (Configuration.defaultConfig())
let sleep (ms : int) = System.Threading.Thread.Sleep(ms)
let timer = Stopwatch()
timer.Start()
let mutable registeredUserList:List<string> = List<string>()
let mutable userStateDic = Dictionary<string, string>()
let mutable GlobalTweetCounter = 0L
type Command =
    | InitUser of string
    | LetFollow of string * string
    | SubscribeFrom of string
    | SubscribeTo of string
    | LetSend of string * string
    | SendTweet of string
    | LetReTweet of string * string
    | ReTweet of string
    | SubscribedTweet of string
    | Mention of string

    | LetQuerBySubs of string * List<string>
    | QuerSubs of List<string>
    | QuerS of List<string>
    | LetQuerByHash of string * List<string>
    | QuerHash of string
    | LetQuerByMent of string * List<string>
    | QuerMent of List<string>
    | RegisterNumerous of int
    | RegisterOne of string * string
    | LetLogin of string * string
    | LetLogout of string
    | GlobalTweet of string
    | QueryHistory of List<string>
(*let In_numNodes = fsi.CommandLineArgs.[1] |> int*)
let twitterUser (mailbox:Actor<_>) =
    //users who following me
    let mutable followerList:List<string> = List<string>()
    //users I follow
    let mutable followingUserList : List<string> = List<string>()
    //subscribed user's tweet
    let mutable subscribedTweetList : List<string> = List<string>()
    //my mentions
    let mutable mymentionTweetList : List<string> = List<string>()
    //tweets I have tweeted
    let mutable myTweetedList : List<string> = List<string>()
    let mutable serverpath = ""
    let mutable myid = ""
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | InitUser(x) -> myid <- x
                         serverpath <- string(sender.Path)
                         //printfn"my name is %s"myid
                         return! loop()
        | SendTweet(ss) ->
                           let thistweet = "(" + myid + "): " + ss
                           myTweetedList.Add(thistweet)
                           if (ss.Contains("@")) then
                              let mention = ss.Split("@")
                              for i in 1..mention.Length - 1 do
                                  let mentionUser = mention.[i].Split(" ")
                                  if userStateDic.ContainsKey(mentionUser.[0]) then
                                     let userpath = "user/" + mentionUser.[0]
                                     select userpath system <! Mention(thistweet)
                           (*for iii in 0..followerList.Count - 1 do
                              printfn"&&&&%s"followerList.[iii]*)
                           select "user/Boss" system <! GlobalTweet(thistweet)
                           for i in 0..followerList.Count - 1 do
                              let userpath =  "user/" + followerList.[i]
                              select userpath system <! SubscribedTweet(thistweet)
                           //printfn"<><>%s tweet %s..."myid ss
                           return! loop()
        | Mention(x) ->
                        mymentionTweetList.Add(x)
                        return! loop()
        | SubscribedTweet(x) ->
                                GlobalTweetCounter <- GlobalTweetCounter + 1L
                                subscribedTweetList.Add(x)
                                return! loop()
        | ReTweet(ss) ->
                        let thisretweet ="("+ myid + " retweeted): " + ss
                        myTweetedList.Add(thisretweet)
                        if (ss.Contains("@")) then
                           let mention = ss.Split("@")
                           for i in 1..mention.Length - 1 do
                               let mentionUser = mention.[i].Split(" ")
                               if userStateDic.ContainsKey(mentionUser.[0]) then
                                  let userpath = "user/" + mentionUser.[0]
                                  select userpath system <! Mention(thisretweet)
                        select "user/Boss" system <! GlobalTweet(thisretweet)
                        for i in 0..followerList.Count - 1 do
                             let userpath =  "user/" + followerList.[i]
                             select userpath system <! SubscribedTweet(thisretweet)
                        return! loop()
        | SubscribeFrom(x) ->  //printfn"now %s becomes one of %s's follower"x myid
                               if (followerList.Contains(x)) then
                                   //printfn"%s already followed me,%s"x myid
                                   return! loop()
                               followerList.Add(x)
                               return! loop()
        | SubscribeTo(x) -> //printfn"now me,%s is following %s"myid x
                            if (followingUserList.Contains(x)) then
                                //printfn"me,%s already subscribed %s"myid x
                                return! loop()
                            followingUserList.Add(x)
                            return! loop()
        | QuerSubs(tmplist) ->
                      for i in 0..followingUserList.Count - 1 do
                          //printfn"----%s"followingUserList.[i]
                          let name = "user/" + followingUserList.[i]
                          select name system <! QuerS(tmplist)
                          sleep(10)
                      (*printfn"%s subscribed tweets are .... {"myid
                      for i in 0..subscribedTweetList.Count - 1 do
                          printfn"%s" subscribedTweetList.[i]
                          tmplist.Add(subscribedTweetList.[i])
                      printfn"******}"*)
                      return! loop()
        | QuerS(tlist) ->
                      for i in 0..myTweetedList.Count - 1 do
                          tlist.Add(myTweetedList.[i])
                      return! loop()
        | QuerMent(tmplist) ->
                      printfn"the result of tweets which mentioned me,(%s) are .... {"myid
                      for i in 0..mymentionTweetList.Count - 1 do
                          printfn"%s" mymentionTweetList.[i]
                          tmplist.Add(mymentionTweetList.[i])
                      printfn"******}"
                      return! loop()
        | _ -> return! loop()
    }
    loop ()

let getTwitterUserList numnodes =
    //let intlist = List<int>()
    let TwitterUserList = List<string>()
    for i = 0 to numnodes - 1 do
        let username = "tu" + string(i) + "_123456"
        TwitterUserList.Add(username)
    TwitterUserList

let boss (mailbox: Actor<_>) =
    let mutable onlineUserList:List<string> = List<string>()
    let mutable HistoryTweetList : List<string> = List<string>()
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        match msg with
        | RegisterNumerous(i) -> let ulist = getTwitterUserList i
                                 for i in 0..ulist.Count - 1 do
                                     registeredUserList.Add(ulist.[i])
                                 for ii in 0.. ulist.Count - 1 do
                                     let up = ulist.[ii].Split("_")
                                     let username = up.[0]
                                     let ref = spawn system username <| twitterUser
                                     ref <! InitUser(username)
                                     userStateDic.Add(username,"active")
                                 return! loop()
        | RegisterOne(x,i) -> let ss = x + "_" + string(i)
                              if(registeredUserList.Contains(ss)) then
                                  printfn"username occupied...."
                                  return! loop()
                              else
                                  registeredUserList.Add(ss)
                                  let ref = spawn system x <| twitterUser
                                  ref <! InitUser(x)
                                  userStateDic.Add(x,"active")
                                  return! loop()
        | LetLogin(x,i) -> //for i in 0..registeredUserList.Count - 1 do
                        if registeredUserList.Contains(x + "_" + string(i)) then
                            if userStateDic.Item(x) = "stop" then
                                userStateDic.Item(x) <- "active"
                                printfn"%s login successfully"x
                            else
                                printfn"already logged in"
                        else
                            printfn"invalid info, please register..."
                        return! loop()
        | LetLogout(x) ->
                       if userStateDic.ContainsKey(x) then
                          userStateDic.Item(x) <- "stop"
                          printfn"%s log out successfully....."x
                       else
                           printfn"user %s is not exists..."x
                       return! loop()
        | LetFollow(x,y) -> //a b   b wants to follow a,  add b to a's follower list,  add a to b's followinglist
                         if (userStateDic.Item(x).Equals("active") && userStateDic.Item(y).Equals("active")) then
                            let name1 = "user/" + x
                            let name2 = "user/" + y
                            select name1 system <! SubscribeFrom(y)
                            select name2 system <! SubscribeTo(x)
                         else
                             printfn"user is offline, subscribe failed, login to subscribe...."
                         return! loop()
        | LetSend(x,ss) ->
                        if userStateDic.Item(x).Equals("active") then
                           let name = "user/" + x
                           select name system <! SendTweet(ss)
                        else
                            printfn"User is offline, send tweet failed, login to tweet...."
                        return! loop()
        | LetReTweet(x ,ss) ->
                        if userStateDic.Item(x).Equals("active") then
                           let name = "user/" + x
                           select name system <! ReTweet(ss)
                           (*let rnd = Random()
                           if(HistoryTweetList.Count > 0) then
                               let rindex = rnd.Next(HistoryTweetList.Count)
                               let rt = HistoryTweetList.[rindex]
                               let name = "user/" + x
                               select name system <! ReTweet(rt)
                           else
                               //printfn"sorry, there's no content to retweet..."
                               ()*)
                        else
                            printfn"User is offline, retweet failed, login to retweet...."
                        return! loop()
        | GlobalTweet(x) -> HistoryTweetList.Add(x)
                            (*printfn"historytweets are..."
                            for i in 0..HistoryTweetList.Count - 1 do
                                printfn"%s"HistoryTweetList.[i]*)
                            return! loop()
        | LetQuerBySubs(x,tmplist) ->
                              let name = "user/" + x
                              select name system <! QuerSubs(tmplist)
                              return! loop()
        | LetQuerByHash(hash, tmplist) ->
                                 printfn"The results of querying by hashtag is...."
                                 let mutable count = 0
                                 for i in 0..HistoryTweetList.Count - 1 do
                                     let hist = HistoryTweetList.[i]
                                     if (hist.Contains(hash)) then
                                         count <- count + 1
                                         printfn"%s"hist
                                         tmplist.Add(hist)
                                 if count = 0 then
                                     printfn"null"
                                 return! loop()
        | LetQuerByMent(x,tmplist) ->
                              let name = "user/" + x
                              select name system <! QuerMent(tmplist)
                              return! loop()
        | QueryHistory(tmplist) ->
                              //tmplist.Clear()
                              for i in 0..HistoryTweetList.Count - 1 do
                                tmplist.Add(HistoryTweetList.[i])
                              return! loop()
        | _ -> return! loop()
    }
    loop()
let Boss = spawn system "Boss" boss
let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    // if `loop` is set to false, the server will stop receiving messages
    let mutable loop = true
    while loop do
      // the server will wait for a message to be received without blocking the thread
      let! msg = webSocket.read()
      match msg with
      | (Text, data, true) ->
        // the message can be converted to a string
        let str = UTF8.toString data
        let clientinfo = str.Split("-->&")
        match clientinfo.[0] with
        | "Reg" ->
           let userReg = clientinfo.[1].Split("_")
           let thisusername = userReg.[0]
           let thispassword = userReg.[1]
           Boss <! RegisterOne(thisusername,thispassword)
           sleep(100)
           let mutable response = ""
           if userStateDic.Item(thisusername).Equals("active") then
              response <- sprintf "%s registered successfully..."thisusername
           else
              response <- sprintf "%s registered failed..."thisusername
          // let second = "second part is..."
           let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
           //let secondpart = second |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
           do! webSocket.send Text byteResponse true
           //do! webSocket.send Continuation secondpart true
        | "LogI" ->
           let userLogi = clientinfo.[1].Split("_")
           let thisusername = userLogi.[0]
           let thispassword = userLogi.[1]
           let beforeflag = userStateDic.Item(thisusername).Equals("active")
           Boss <! LetLogin(thisusername,thispassword)
           sleep(100)
           let mutable response = ""
           if userStateDic.ContainsKey(thisusername) then
              if beforeflag = false && userStateDic.Item(thisusername).Equals("active") then
                response <- sprintf "%s login successfully..."thisusername
              else
                response <- sprintf "%s login failed..."thisusername
           else
              response <- sprintf "there's no %s, need to register first..."thisusername
           let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
           do! webSocket.send Text byteResponse true
        | "Tweet" ->
           let mycontent = clientinfo.[1].Split("->&")
           let myuserid = mycontent.[0]
           let mytweet = mycontent.[1]
           Boss <! LetSend(myuserid, mytweet)
           let mutable response = ""
           response <- sprintf "(%s): %s"myuserid mytweet
           //if myusermsg
           let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
           do! webSocket.send Text byteResponse true

        | "ReTweet" ->
           let mycontent = clientinfo.[1].Split("->&")
           let myuserid = mycontent.[0]
           let myretweet = mycontent.[1]
           let mutable alltweetlist:List<string> = List<string>()
           Boss <! QueryHistory(alltweetlist)
           sleep(100)
           //for i in alltweetlist
           if alltweetlist.Contains(myretweet) then
             let mutable response = ""
             Boss <! LetReTweet(myuserid, myretweet)
             sleep(10)
             response <- sprintf "(%s retweeted): %s"myuserid myretweet
             let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
             do! webSocket.send Text byteResponse true

        | "Sub" ->
           let mycontent = clientinfo.[1].Split("->&")
           let myuserid = mycontent.[0]
           let mysubto = mycontent.[1]
           Boss <! LetFollow(mysubto, myuserid)
           let mutable response = ""
           response <- sprintf "subscribed to %s"mysubto
           let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
           do! webSocket.send Text byteResponse true

        | "QueH" ->
           let mycontent = clientinfo.[1].Split("->&")
           //let myuserid = mycontent.[0]
           let myhash = mycontent.[1]
           let mutable qhlist:List<string> = List<string>()
           Boss <! LetQuerByHash(myhash,qhlist)
           sleep(100)
           for i in 0..qhlist.Count - 1 do
             let mutable response = qhlist.[i]
             let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
             do! webSocket.send Text byteResponse true

        | "QueM" ->
           let myuserid = clientinfo.[1]
           let mutable qmlist:List<string> = List<string>()
           Boss <! LetQuerByMent(myuserid,qmlist)
           sleep(100)
           for i in 0..qmlist.Count - 1 do
             let mutable response = qmlist.[i]
             let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
             do! webSocket.send Text byteResponse true

        | "QueS" ->
           let myuserid = clientinfo.[1]
           let mutable qmlist:List<string> = List<string>()
           Boss <! LetQuerBySubs(myuserid,qmlist)
           sleep(100)
           for i in 0..qmlist.Count - 1 do
             let mutable response = qmlist.[i]
             let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
             do! webSocket.send Text byteResponse true

        | "LogO" ->
           let myuserid = clientinfo.[1]
           Boss <! LetLogout(myuserid)
           let mutable response = ""
           response <- sprintf "%s logout successfully"myuserid
           let byteResponse = response |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
           do! webSocket.send Text byteResponse true

        | _ -> ()

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true
        // after sending a Close message, stop the loop
        loop <- false
      | _ -> ()
    }
let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) =
   let exampleDisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource needed by websocket connection disposed" }
   let websocketWorkflow = ws webSocket context
   async {
    let! successOrError = websocketWorkflow
    match successOrError with
    // Success case
    | Choice1Of2() -> ()
    // Error case
    | Choice2Of2(error) ->
        // Example error handling logic here
        printfn "Error: [%A]" error
        exampleDisposableResource.Dispose()
    return successOrError
   }
let app : WebPart =
  choose [
    path "/websocket" >=> handShake ws
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Found no handlers." ]

[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0