using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;

var serviceProvider = new ServiceCollection()
    .AddLogging(options => options.AddConsole())
    .AddTransient<MyService>()
    .BuildServiceProvider();

var handler = new MessageHandler(serviceProvider);

handler.Map((PingMessage message, ILogger<PingMessage> logger) =>
{
    logger.LogInformation("Ping handler");
    return Task.CompletedTask;
});

handler.Map(async (PongMessage message, MyService myService, ILogger<PongMessage> logger, CancellationToken cancellationToken) =>
{
    await myService.MyMethod(cancellationToken);
    logger.LogInformation("Pong handler");
});

await handler.HandleAsync(new PingMessage());
await handler.HandleAsync(new PongMessage());

class MyService
{
    public async Task MyMethod(CancellationToken cancellationToken = default) => await Task.Delay(1000, cancellationToken);
}

class PingMessage : IMessage { }
class PongMessage : IMessage { }

interface IMessage { }

class MessageHandler
{
    private static readonly MethodInfo GetRequiredServiceMethod = typeof(ServiceProviderServiceExtensions).GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(IServiceProvider) })!;

    private static readonly ParameterExpression TargetExpr = Expression.Parameter(typeof(object), "target");
    private static readonly ParameterExpression ServiceProviderExpr = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
    private static readonly ParameterExpression MessageExpr = Expression.Parameter(typeof(IMessage), "message");
    private static readonly ParameterExpression CancellationTokenExpr = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, Func<IServiceProvider, IMessage, CancellationToken, Task>> _handlers = new();

    public MessageHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public MessageHandler Map(Delegate handler)
    {
        var parameters = handler.Method.GetParameters();

        var args = new Expression[parameters.Length];

        Type? messageType = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (parameters[i].ParameterType.IsAssignableTo(typeof(IMessage)))
            {
                messageType = parameters[i].ParameterType;
                args[i] = Expression.Convert(MessageExpr, parameters[i].ParameterType);
            }
            else if (parameters[i].ParameterType == typeof(CancellationToken))
            {
                args[i] = CancellationTokenExpr;
            }
            else
            {
                args[i] = Expression.Call(GetRequiredServiceMethod.MakeGenericMethod(parameters[i].ParameterType), ServiceProviderExpr);
            }
        }

        if (messageType is null)
        {
            throw new InvalidOperationException("No message parameter found");
        }

        var targetExpression = handler.Target switch
        {
            object => Expression.Convert(TargetExpr, handler.Target.GetType()),
            null => null,
        };

        var lambda = Expression.Lambda<Func<object?, IServiceProvider, IMessage, CancellationToken, Task>>(
            Expression.Call(targetExpression, handler.Method, args),
            TargetExpr, ServiceProviderExpr, MessageExpr, CancellationTokenExpr);

        var method = lambda.Compile();

        _handlers[messageType] = async (IServiceProvider serviceProvider, IMessage message, CancellationToken cancellationToken) =>
            await method(handler.Target, serviceProvider, message, cancellationToken);

        return this;
    }

    public async Task HandleAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(message.GetType(), out var handler))
        {
            throw new InvalidOperationException($"No handler found for {message.GetType()}");
        }

        await handler(_serviceProvider, message, cancellationToken);
    }
}
