open System
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Giraffe
open Fake.Core
open Microsoft.AspNetCore.Http

let predict inputStream =
    let resultPrefix = "result: "

    CreateProcess.fromRawCommandLine "python" "./PythonModel/model1.py"
    |> CreateProcess.withStandardInput (UseStream (false, inputStream))
    |> CreateProcess.redirectOutput
    |> CreateProcess.map (fun processResult -> processResult.Result.Output)
    |> CreateProcess.withTimeout (TimeSpan.FromSeconds(1.))
    |> Proc.run
    |> String.splitStr Environment.NewLine
    |> Seq.find (fun line -> line.StartsWith(resultPrefix))
    |> String.replaceFirst resultPrefix String.Empty

let handlePredictionRequest (context : HttpContext) =
    try
        let prediction = predict context.Request.Body

        context.SetContentType ("application/json")
        context.WriteStringAsync prediction
    with :? TimeoutException ->
        context.SetStatusCode StatusCodes.Status504GatewayTimeout
        context.WriteJsonAsync {| error = "model timeout" |}


let webApp =
    route "/predict" >=> handleContext(handlePredictionRequest)

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseUrls("http://localhost:8080/")
                    .Configure(fun app -> app.UseGiraffe webApp)
                    .ConfigureServices(fun services -> services.AddGiraffe() |> ignore)
                    |> ignore)
        .Build()
        .Run()
    0