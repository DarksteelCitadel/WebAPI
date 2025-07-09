using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// Error-handling middleware (first)
app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var errorResponse = JsonSerializer.Serialize(new { error = "Internal server error." });
        await context.Response.WriteAsync(errorResponse);
        Console.WriteLine($"Exception: {ex.Message}");
    }
});

// Authentication middleware (second)
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (authHeader == null || !authHeader.StartsWith("Bearer "))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    var token = authHeader.Substring("Bearer ".Length).Trim();

    // Simple token validation (replace with real validation in production)
    if (token != "mysecrettoken")
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    await next.Invoke();
});

// Logging middleware (last)
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var path = context.Request.Path;
    await next.Invoke();
    var statusCode = context.Response.StatusCode;
    Console.WriteLine($"HTTP {method} {path} responded {statusCode}");
});

// Simple in-memory data store
var items = new List<string>();

// CREATE
app.MapPost("/items", (string item) =>
{
    // Validation: ensure item is not null or empty
    if (string.IsNullOrWhiteSpace(item))
    {
        return Results.BadRequest(new { error = "Item cannot be empty." });
    }

    try
    {
        items.Add(item);
        return Results.Created($"/items/{items.Count - 1}", item);
    }
    catch (Exception ex)
    {
        // Handle unexpected errors
        return Results.Problem("An error occurred while creating the item.");
    }
});

// READ ALL
app.MapGet("/items", () =>
{
    try
    {
        // Optimization: return a copy to avoid exposing internal list
        return Results.Ok(items.ToList());
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while retrieving items.");
    }
});

// READ ONE
app.MapGet("/items/{id:int}", (int id) =>
{
    try
    {
        if (id < 0 || id >= items.Count) return Results.NotFound(new { error = "Item not found." });
        return Results.Ok(items[id]);
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while retrieving the item.");
    }
});

// UPDATE
app.MapPut("/items/{id:int}", (int id, string updatedItem) =>
{
    // Validation: ensure updatedItem is not null or empty
    if (string.IsNullOrWhiteSpace(updatedItem))
    {
        return Results.BadRequest(new { error = "Updated item cannot be empty." });
    }

    try
    {
        if (id < 0 || id >= items.Count) return Results.NotFound(new { error = "Item not found." });
        items[id] = updatedItem;
        return Results.NoContent();
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while updating the item.");
    }
});

// DELETE
app.MapDelete("/items/{id:int}", (int id) =>
{
    try
    {
        if (id < 0 || id >= items.Count) return Results.NotFound(new { error = "Item not found." });
        items.RemoveAt(id);
        return Results.NoContent();
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while deleting the item.");
    }
});

app.MapGet("/", () => "Hello, ASP.NET Core Middleware!");

app.Run();