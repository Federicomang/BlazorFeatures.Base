using BlazorFeatures.Abstractions.Interfaces;
using BlazorFeatures.Abstractions.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using ServerHttpTools = BlazorFeatures.Base.Server.Tools.HttpTools;

namespace BlazorFeatures.Base.Tests;

public class HttpBindingTests
{
    [Fact]
    public async Task Query_string_round_trip_supports_scalars_arrays_complex_objects_and_extra_data()
    {
        var source = new BindingModel
        {
            Name = "Federico",
            Numbers = [2, 3, 4],
            Detail = new DetailModel { Enabled = true, Label = "test" },
            OtherData = new Dictionary<string, JsonElement>
            {
                ["pino"] = JsonSerializer.SerializeToElement("asd"),
                ["ciao"] = JsonSerializer.SerializeToElement(new[] { 2, 3, 4 })
            }
        };

        var encoded = ServerHttpTools.ToQueryString(source);
        var parsed = QueryHelpers.ParseQuery(encoded);

        Assert.Equal(["2", "3", "4"], parsed["numbers"].Select(value => value!).ToArray());
        Assert.Single(parsed["detail"]);
        Assert.Equal(["2", "3", "4"], parsed["ciao"].Select(value => value!).ToArray());
        Assert.Equal("asd", parsed["pino"]);

        var context = CreateContext();
        context.Request.QueryString = new QueryString("?" + encoded);

        var bound = await BlazorFeatures.Base.Server.Tools.QueryBound<BindingModel>
            .BindAsync(context, null!);
        var model = bound!.Value;

        Assert.Equal("Federico", model.Name);
        Assert.Equal([2, 3, 4], model.Numbers!);
        Assert.Equal("test", model.Detail!.Label);
        Assert.True(model.Detail.Enabled);
        Assert.Equal("asd", model.OtherData!["pino"].GetString());
        Assert.Equal(
            [2, 3, 4],
            model.OtherData["ciao"].EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task Repeated_unmanaged_query_values_become_a_typed_json_array()
    {
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?pino=asd&ciao=2&ciao=3&ciao=4");

        var bound = await BlazorFeatures.Base.Server.Tools.QueryBound<UnmanagedOnlyModel>
            .BindAsync(context, null!);
        var data = bound!.Value.OtherData!;

        Assert.Equal("asd", data["pino"].GetString());
        Assert.Equal(
            [2, 3, 4],
            data["ciao"].EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task Form_binding_supports_repeated_arrays_complex_objects_and_unmanaged_values()
    {
        var context = CreateContext();
        var body = "numbers=2&numbers=3&numbers=4" +
            "&detail=%7B%22enabled%22%3Atrue%2C%22label%22%3A%22test%22%7D" +
            "&pino=asd&ciao=2&ciao=3&ciao=4";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var bound = await BlazorFeatures.Base.Server.Tools.FormBound<BindingModel>
            .BindAsync(context);
        var model = bound!.Value;

        Assert.Equal([2, 3, 4], model.Numbers!);
        Assert.Equal("test", model.Detail!.Label);
        Assert.Equal("asd", model.OtherData!["pino"].GetString());
        Assert.Equal(
            [2, 3, 4],
            model.OtherData["ciao"].EnumerateArray().Select(item => item.GetInt32()).ToArray());
    }

    [Fact]
    public async Task Optional_type_hints_override_inference_for_unmanaged_query_values()
    {
        var created = new DateTimeOffset(2026, 9, 11, 14, 30, 0, TimeSpan.FromHours(2));
        var source = new UnmanagedOnlyModel
        {
            OtherData = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(2),
                ["created"] = JsonSerializer.SerializeToElement(created),
                ["ids"] = JsonSerializer.SerializeToElement(new[] { "001", "002" })
            }
        };
        var hints = new Dictionary<string, string>
        {
            ["code"] = UrlEncodedValueTypes.String,
            ["created"] = UrlEncodedValueTypes.DateTimeOffset,
            ["ids"] = UrlEncodedValueTypes.Int32
        };

        var encoded = ServerHttpTools.ToQueryString(
            source,
            jsonOptions: null,
            typeHints: hints);
        var parsed = QueryHelpers.ParseQuery(encoded);

        Assert.Equal("string", parsed["$type:code"]);
        Assert.Equal("datetimeoffset", parsed["$type:created"]);

        var context = CreateContext();
        context.Request.QueryString = new QueryString("?" + encoded);
        var bound = await BlazorFeatures.Base.Server.Tools.QueryBound<UnmanagedOnlyModel>
            .BindAsync(context, null!);
        var data = bound!.Value.OtherData!;

        Assert.Equal("2", data["code"].GetString());
        Assert.Equal(created, data["created"].GetDateTimeOffset());
        Assert.Equal(
            [1, 2],
            data["ids"].EnumerateArray().Select(item => item.GetInt32()).ToArray());
        Assert.DoesNotContain("$type:code", data.Keys);
    }

    [Fact]
    public async Task Explicit_array_hint_preserves_an_array_with_one_value()
    {
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?id=7&%24type%3Aid=int%5B%5D");

        var bound = await BlazorFeatures.Base.Server.Tools.QueryBound<UnmanagedOnlyModel>
            .BindAsync(context, null!);
        var value = bound!.Value.OtherData!["id"];

        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Equal(7, value[0].GetInt32());
    }

    [Fact]
    public async Task Form_type_hints_are_applied_to_unmanaged_values()
    {
        var context = CreateContext();
        var body = "code=002&%24type%3Acode=string";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var bound = await BlazorFeatures.Base.Server.Tools.FormBound<UnmanagedOnlyModel>
            .BindAsync(context);

        Assert.Equal("002", bound!.Value.OtherData!["code"].GetString());
    }

    [Fact]
    public async Task Arbitrary_clr_types_are_rejected()
    {
        var context = CreateContext();
        context.Request.QueryString = new QueryString(
            "?value=2&%24type%3Avalue=System.Int32");

        await Assert.ThrowsAsync<BadHttpRequestException>(async () =>
            await BlazorFeatures.Base.Server.Tools.QueryBound<UnmanagedOnlyModel>
                .BindAsync(context, null!));
    }

    [Fact]
    public async Task Type_hints_cannot_override_declared_properties()
    {
        var context = CreateContext();
        context.Request.QueryString = new QueryString(
            "?name=Federico&%24type%3Aname=int");

        await Assert.ThrowsAsync<BadHttpRequestException>(async () =>
            await BlazorFeatures.Base.Server.Tools.QueryBound<BindingModel>
                .BindAsync(context, null!));
    }

    [Fact]
    public async Task Multipart_round_trip_uses_the_same_value_rules_and_supports_files()
    {
        var fileBytes = Encoding.UTF8.GetBytes("file content");
        var source = new MultipartSourceModel
        {
            Name = "Federico",
            Numbers = [2, 3, 4],
            Detail = new DetailModel { Enabled = true, Label = "multipart" },
            Document = new MultipartFileData(
                new MemoryStream(fileBytes),
                "document.txt",
                "text/plain",
                new Dictionary<string, string[]>
                {
                    ["X-Document-Id"] = ["42"],
                    ["X-Multiple"] = ["one", "two"]
                }),
            Attachments =
            [
                new MultipartFileData(new MemoryStream([1]), "one.bin"),
                new MultipartFileData(new MemoryStream([2]), "two.bin")
            ],
            OtherData = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(2)
            }
        };
        var hints = new Dictionary<string, string>
        {
            ["code"] = UrlEncodedValueTypes.String
        };

        using var multipart = ServerHttpTools.ToMultipartFormDataContent(
            source,
            jsonOptions: null,
            typeHints: hints);
        var requestBody = new MemoryStream();
        await multipart.CopyToAsync(requestBody);
        requestBody.Position = 0;

        var context = CreateContext();
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.ContentLength = requestBody.Length;
        context.Request.Body = requestBody;

        var bound = await BlazorFeatures.Base.Server.Tools.FormBound<MultipartTargetModel>
            .BindAsync(context);
        var model = bound!.Value;

        Assert.Equal("Federico", model.Name);
        Assert.Equal([2, 3, 4], model.Numbers!);
        Assert.Equal("multipart", model.Detail!.Label);
        Assert.Equal("2", model.OtherData!["code"].GetString());
        Assert.Equal("document.txt", model.Document!.FileName);
        Assert.Equal("text/plain", model.Document.ContentType);
        Assert.Equal(["42"], model.Document.Headers["X-Document-Id"]);
        Assert.Equal(["one, two"], model.Document.Headers["X-Multiple"]);
        Assert.Contains("Content-Disposition", model.Document.Headers.Keys);
        Assert.Equal(["one.bin", "two.bin"], model.Attachments!.Select(file => file.FileName));

        using var reader = new StreamReader(model.Document.Content);
        Assert.Equal("file content", await reader.ReadToEndAsync());
    }

    private static DefaultHttpContext CreateContext()
    {
        var services = new ServiceCollection()
            .AddOptions()
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class BindingModel : IWithUnmanagedData
    {
        public string? Name { get; set; }
        public int[]? Numbers { get; set; }
        public DetailModel? Detail { get; set; }
        public Dictionary<string, JsonElement>? OtherData { get; set; }
    }

    private sealed class UnmanagedOnlyModel : IWithUnmanagedData
    {
        public Dictionary<string, JsonElement>? OtherData { get; set; }
    }

    private sealed class DetailModel
    {
        public bool Enabled { get; set; }
        public string? Label { get; set; }
    }

    private sealed class MultipartSourceModel : IWithUnmanagedData
    {
        public string? Name { get; set; }
        public int[]? Numbers { get; set; }
        public DetailModel? Detail { get; set; }
        public MultipartFileData? Document { get; set; }
        public MultipartFileData[]? Attachments { get; set; }
        public Dictionary<string, JsonElement>? OtherData { get; set; }
    }

    private sealed class MultipartTargetModel : IWithUnmanagedData
    {
        public string? Name { get; set; }
        public int[]? Numbers { get; set; }
        public DetailModel? Detail { get; set; }
        public MultipartFileData? Document { get; set; }
        public MultipartFileData[]? Attachments { get; set; }
        public Dictionary<string, JsonElement>? OtherData { get; set; }
    }
}
