# Async Operations in Orleans.StateMachineES

## Important: Understanding Stateless Limitations

The underlying Stateless library **does not support async operations** in state callbacks (OnEntry, OnExit, OnEntryFrom, OnExitTo). This is a fundamental design limitation of Stateless, not Orleans.StateMachineES.

## ❌ Common Mistakes to Avoid

### 1. Don't Use Async Lambdas in Callbacks

```csharp
// ❌ WRONG - This will NOT work correctly
machine.Configure(OrderState.Processing)
    .OnEntry(async () => 
    {
        // This async operation will run as fire-and-forget!
        await SaveToDatabase();  // NOT AWAITED!
        await SendEmail();       // NOT AWAITED!
    });

// ❌ WRONG - Don't call FireAsync from callbacks
machine.Configure(OrderState.Processing)
    .OnEntry(() => 
    {
        // This will throw InvalidOperationException at runtime
        FireAsync(OrderTrigger.Complete).Wait();  // DEADLOCK or EXCEPTION!
    });
```

### 2. Don't Block on Async Operations

```csharp
// ❌ WRONG - This will cause deadlocks
machine.Configure(OrderState.Processing)
    .OnEntry(() => 
    {
        SaveToDatabase().Wait();  // DEADLOCK!
        Task.Run(async () => await SendEmail()).Wait();  // DEADLOCK!
    });
```

## ✅ Correct Patterns

### Pattern 1: Perform Async Operations in Grain Methods

The recommended approach is to perform async operations in your grain methods, not in state callbacks:

```csharp
public class OrderGrain : StateMachineGrain<OrderState, OrderTrigger>, IOrderGrain
{
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Created);
        
        // Configure states with SYNCHRONOUS callbacks only
        machine.Configure(OrderState.Created)
            .Permit(OrderTrigger.Submit, OrderState.Processing)
            .OnExit(() => LogTransition("Leaving Created state"));  // Synchronous logging
        
        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Complete, OrderState.Completed)
            .OnEntry(() => LogTransition("Entering Processing state"))  // Synchronous logging
            .OnExit(() => LogTransition("Leaving Processing state"));
        
        return machine;
    }
    
    // Grain method that handles async operations
    public async Task SubmitOrderAsync(OrderData data)
    {
        // 1. Perform async operations BEFORE state transition
        await ValidateOrderAsync(data);
        await SaveToDatabase(data);
        
        // 2. Fire the trigger (state transition)
        await FireAsync(OrderTrigger.Submit);
        
        // 3. Perform async operations AFTER state transition
        await SendConfirmationEmailAsync(data);
        await NotifyInventoryServiceAsync(data);
    }
    
    private void LogTransition(string message)
    {
        // Synchronous logging is fine
        Console.WriteLine($"[{DateTime.UtcNow}] {message}");
    }
}
```

### Pattern 2: Use State Change Notifications

If you need to react to state changes with async operations, use a separate method:

```csharp
public class OrderGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger, OrderGrainState>, IOrderGrain
{
    private readonly Queue<Func<Task>> _postTransitionTasks = new();
    
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Created);
        
        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Complete, OrderState.Completed)
            .OnEntry(() => 
            {
                // Queue async work to be done after transition
                _postTransitionTasks.Enqueue(() => SendProcessingNotificationAsync());
            });
        
        return machine;
    }
    
    public override async Task FireAsync(OrderTrigger trigger)
    {
        // Fire the trigger
        await base.FireAsync(trigger);
        
        // Execute any queued async operations after transition
        while (_postTransitionTasks.Count > 0)
        {
            var task = _postTransitionTasks.Dequeue();
            await task();
        }
    }
    
    private async Task SendProcessingNotificationAsync()
    {
        await _notificationService.NotifyAsync("Order is being processed");
    }
}
```

### Pattern 3: State-Specific Grain Methods

Create grain methods that encapsulate both the state transition and related async operations:

```csharp
public interface IOrderGrain : IGrainWithStringKey
{
    Task<OrderSubmissionResult> SubmitOrderAsync(OrderData data);
    Task<ProcessingResult> ProcessOrderAsync();
    Task<CompletionResult> CompleteOrderAsync();
}

public class OrderGrain : StateMachineGrain<OrderState, OrderTrigger>, IOrderGrain
{
    public async Task<OrderSubmissionResult> SubmitOrderAsync(OrderData data)
    {
        // Validate current state
        if (!await CanFireAsync(OrderTrigger.Submit))
        {
            return new OrderSubmissionResult 
            { 
                Success = false, 
                Message = "Order cannot be submitted in current state" 
            };
        }
        
        // Perform async pre-transition operations
        var validationResult = await ValidateOrderAsync(data);
        if (!validationResult.IsValid)
        {
            return new OrderSubmissionResult 
            { 
                Success = false, 
                Message = validationResult.Message 
            };
        }
        
        // State transition
        await FireAsync(OrderTrigger.Submit);
        
        // Perform async post-transition operations
        await SaveOrderAsync(data);
        await SendConfirmationEmailAsync(data);
        
        return new OrderSubmissionResult 
        { 
            Success = true, 
            OrderId = this.GetPrimaryKeyString() 
        };
    }
}
```

## Compile-Time Safety

Orleans.StateMachineES includes Roslyn analyzers that detect common async mistakes at compile time:

### OSMES001: Async Lambda in Callback

```csharp
// This will generate a compiler warning
machine.Configure(State.Active)
    .OnEntry(async () => await DoSomething());  // ⚠️ Warning OSMES001
```

### OSMES002: FireAsync in Callback

```csharp
// This will generate a compiler error
machine.Configure(State.Active)
    .OnEntry(() => 
    {
        FireAsync(Trigger.Next);  // ❌ Error OSMES002
    });
```

## Best Practices

1. **Keep callbacks simple and synchronous**: Use them only for logging, metrics, or updating local state
2. **Perform async operations in grain methods**: This gives you full control over error handling and ordering
3. **Validate before transitioning**: Check if a trigger can fire before performing expensive operations
4. **Use event sourcing for reliability**: EventSourcedStateMachineGrain handles async persistence automatically
5. **Document your state machine flow**: Make it clear where async operations occur in your workflow

## Example: Complete Order Processing Workflow

```csharp
public class OrderProcessingGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger, OrderGrainState>, IOrderGrain
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPaymentService _paymentService;
    
    public OrderProcessingGrain(
        IOrderRepository repository,
        IEmailService emailService,
        IPaymentService paymentService)
    {
        _repository = repository;
        _emailService = emailService;
        _paymentService = paymentService;
    }
    
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(() => State.CurrentState);
        
        machine.Configure(OrderState.Created)
            .Permit(OrderTrigger.Submit, OrderState.Validating);
        
        machine.Configure(OrderState.Validating)
            .Permit(OrderTrigger.Approve, OrderState.PaymentPending)
            .Permit(OrderTrigger.Reject, OrderState.Rejected);
        
        machine.Configure(OrderState.PaymentPending)
            .Permit(OrderTrigger.PaymentReceived, OrderState.Processing)
            .Permit(OrderTrigger.PaymentFailed, OrderState.PaymentFailed);
        
        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Ship, OrderState.Shipped)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);
        
        machine.Configure(OrderState.Shipped)
            .Permit(OrderTrigger.Deliver, OrderState.Delivered);
        
        return machine;
    }
    
    public async Task<OrderResult> CreateOrderAsync(CreateOrderCommand command)
    {
        // Initialize state
        State.OrderId = command.OrderId;
        State.CustomerId = command.CustomerId;
        State.Items = command.Items;
        State.CurrentState = OrderState.Created;
        
        // Save to repository
        await _repository.CreateOrderAsync(State);
        
        // Send confirmation email
        await _emailService.SendOrderCreatedEmailAsync(State.CustomerId, State.OrderId);
        
        return new OrderResult { Success = true, OrderId = State.OrderId };
    }
    
    public async Task<OrderResult> SubmitForValidationAsync()
    {
        // Check if we can transition
        if (!await CanFireAsync(OrderTrigger.Submit))
        {
            return new OrderResult 
            { 
                Success = false, 
                Message = $"Cannot submit order in state {State.CurrentState}" 
            };
        }
        
        // Transition to Validating
        await FireAsync(OrderTrigger.Submit);
        
        // Perform validation asynchronously
        var validationResult = await ValidateOrderAsync();
        
        if (validationResult.IsValid)
        {
            await FireAsync(OrderTrigger.Approve);
            await _emailService.SendOrderApprovedEmailAsync(State.CustomerId, State.OrderId);
            
            // Initiate payment process
            var paymentResult = await _paymentService.ProcessPaymentAsync(State);
            
            if (paymentResult.Success)
            {
                await FireAsync(OrderTrigger.PaymentReceived);
                
                // Start processing
                await StartOrderProcessingAsync();
            }
            else
            {
                await FireAsync(OrderTrigger.PaymentFailed);
                await _emailService.SendPaymentFailedEmailAsync(State.CustomerId, State.OrderId);
            }
        }
        else
        {
            await FireAsync(OrderTrigger.Reject);
            await _emailService.SendOrderRejectedEmailAsync(
                State.CustomerId, 
                State.OrderId, 
                validationResult.Reasons);
        }
        
        return new OrderResult 
        { 
            Success = true, 
            OrderId = State.OrderId, 
            State = State.CurrentState 
        };
    }
    
    private async Task<ValidationResult> ValidateOrderAsync()
    {
        // Async validation logic
        var inventoryCheck = await _repository.CheckInventoryAsync(State.Items);
        var creditCheck = await _paymentService.CheckCreditAsync(State.CustomerId);
        
        return new ValidationResult
        {
            IsValid = inventoryCheck && creditCheck,
            Reasons = GenerateValidationReasons(inventoryCheck, creditCheck)
        };
    }
    
    private async Task StartOrderProcessingAsync()
    {
        // Queue for fulfillment
        await _repository.QueueForFulfillmentAsync(State.OrderId);
        
        // Notify warehouse
        await _emailService.NotifyWarehouseAsync(State.OrderId, State.Items);
    }
}
```

## Summary

- **Stateless callbacks are synchronous**: This is by design and cannot be changed
- **Use grain methods for async operations**: This is the Orleans way
- **Leverage compile-time analyzers**: They'll catch common mistakes
- **Follow the patterns**: They ensure reliable and maintainable code
- **Event sourcing handles persistence**: You don't need async in callbacks for persistence

Remember: The separation between synchronous state configuration and asynchronous execution logic is intentional and leads to cleaner, more maintainable code.