using Microsoft.Extensions.FileProviders;
using Microsoft.VisualBasic;
using MongoDB.Bson;
using MongoDB.Driver;
using server;
using server.Collections;

var dbs = new DBService();
var db = await dbs.Start();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var fspath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCafe/fs");
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(fspath),
    RequestPath = "/fs"
});

app.MapGet("/", () => "Wilkommen auf OpenCafe!");

// Customer-related API requests
app.MapPut("/api/customer/register", 
    async (Customers.RegisterRequest request) => await Customers.Register(request, db));

app.MapGet("/api/customer/login", 
    async (HttpRequest request) => 
    {
        var req = new Customers.EmailPasswordRequest(request.Query["email"], request.Query["password"]);
        var res = await Customers.Login(req, db);
        return res;
    });

// Admin-related API requests.

app.MapGet("/api/admin/login", 
    async (HttpRequest request) => 
    {
        var req = new Admins.LoginRequest(request.Query["token"]);
        var res = await Admins.Login(req, db);
        return res;
    });

app.MapPut("/api/admin/register", 
    async (Admins.RegisterRequest request) => await Admins.Register(request, db));

app.MapPut("/api/admin/changename", 
    async (Admins.ChangeNameRequest request) => await Admins.ChangeName(request, db));

app.MapDelete("/api/admin/delete", 
    async (HttpRequest request) => 
    {
        var req = new Admins.DeleteRequest(request.Query["token1"], request.Query["token2"]);
        return await Admins.Delete(req, db);
    });

app.MapGet("/api/admin/getAll", 
    async (HttpRequest request) =>
    {
        var req = new Admins.GetAllRequest(request.Query["token"]);
        return await Admins.GetAll(req, db);
    });

// Image-related API requests.

app.MapPut("/api/images/upload",
    async (HttpContext ctx) => 
    {
        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            var req = new Images.UploadRequest(form.Files["image"], form["author"], form["alt"]);
            return await Images.Upload(req, db);
        }
        return Results.BadRequest();
    });

app.MapDelete("/api/images/delete", 
    async (HttpRequest request) => 
    {
        var req = new Images.DeleteRequest(ObjectId.Parse(request.Query["id"]), request.Query["token"]);
        return await Images.Delete(req, db);
    });

app.MapGet("/api/images/getAll",
     async () => { return await Images.GetAll(db); } );

app.MapGet("/api/images/get", 
    async (HttpRequest request) => { return await Images.GetOne(ObjectId.Parse(request.Query["id"]), db); });

app.Run();



