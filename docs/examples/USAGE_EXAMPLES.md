# uContract.NET Usage Examples

Practical examples demonstrating how to use uContract.NET in real-world scenarios.

> **Note**: Examples use fictional domain classes for illustration.
> For compilable, tested code, see the [test suite](../../tests/uContract.Tests/).

---

## Table of Contents

- [Basic Examples](#basic-examples)
  - [Simple Validation](#simple-validation)
  - [Null Checks](#null-checks)
  - [String Validation](#string-validation)
- [State Capture and Comparison](#state-capture-and-comparison)
  - [Capturing Primitive Values](#capturing-primitive-values)
  - [Capturing Complex Objects](#capturing-complex-objects)
  - [Field Assignment Validation](#field-assignment-validation)
- [Domain-Driven Design Patterns](#domain-driven-design-patterns)
  - [Aggregate Root with Invariants](#aggregate-root-with-invariants)
  - [Value Object](#value-object)
  - [Domain Service](#domain-service)
- [Advanced Patterns](#advanced-patterns)
  - [Method Chaining](#method-chaining)
  - [Immutable Collections](#immutable-collections)
  - [Logical Operators](#logical-operators)
  - [Early Return Pattern](#early-return-pattern)
- [Real-World Scenarios](#real-world-scenarios)
  - [Banking System](#banking-system)
  - [E-Commerce Order Processing](#e-commerce-order-processing)
  - [User Management](#user-management)
  - [Inventory System](#inventory-system)
- [Configuration Examples](#configuration-examples)
- [Testing with Contracts](#testing-with-contracts)

---

## Basic Examples

### Simple Validation

Basic precondition and postcondition validation.

```csharp
using uContract;

public class Calculator
{
    public int Divide(int numerator, int denominator)
    {
        // Precondition: Denominator cannot be zero
        Contract.Require("Denominator must be non-zero", () => denominator != 0);

        int result = numerator / denominator;

        // Postcondition: Result is mathematically correct
        Contract.Ensure("Division result correct", () => result * denominator == numerator);

        return result;
    }

    public int Add(int a, int b)
    {
        int result = a + b;

        // Postcondition: Result is sum of inputs
        Contract.Ensure("Sum is correct", () => result == a + b);

        return result;
    }
}
```

### Null Checks

Using convenience methods for null validation.

```csharp
using uContract;

public class UserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        // Precondition: Repository must not be null
        Contract.RequireNotNull("Repository", repository);

        _repository = repository;
    }

    public User GetUser(string userId)
    {
        // Precondition: User ID must not be null or empty
        Contract.RequireNotEmpty("User ID", userId);

        User? user = _repository.FindById(userId);

        // Postcondition: User must be found (fail-fast pattern)
        Contract.EnsureNotNull("User must exist", user);

        return user;
    }

    public User? TryGetUser(string userId)
    {
        Contract.RequireNotEmpty("User ID", userId);

        // No postcondition - method allows null return
        return _repository.FindById(userId);
    }
}
```

### String Validation

Validating string inputs with contracts.

```csharp
using uContract;

public class EmailService
{
    public void SendEmail(string to, string subject, string body)
    {
        // Preconditions: All parameters must be valid
        Contract.RequireNotEmpty("Recipient email", to);
        Contract.RequireNotEmpty("Email subject", subject);
        Contract.RequireNotEmpty("Email body", body);
        Contract.Require("Valid email format", () => to.Contains("@"));

        // Send email logic
        Console.WriteLine($"Sending email to {to}");
    }

    public void SendBulkEmail(string[] recipients, string subject, string body)
    {
        Contract.RequireNotNull("Recipients", recipients);
        Contract.Require("At least one recipient", () => recipients.Length > 0);
        Contract.RequireNotEmpty("Subject", subject);
        Contract.RequireNotEmpty("Body", body);

        // Precondition: All recipients must have valid email format
        Contract.Require("All emails valid",
            () => recipients.All(email => email.Contains("@")));

        foreach (var recipient in recipients)
        {
            SendEmail(recipient, subject, body);
        }

        // Postcondition: All emails sent
        Contract.Check("All emails sent", () => true);  // Simplified example
    }
}
```

---

## State Capture and Comparison

### Capturing Primitive Values

Using `Old<T>()` to capture state for postcondition validation.

```csharp
using uContract;

public class Counter
{
    private int _count;

    public void Increment()
    {
        // Capture state BEFORE modification
        var oldCount = Contract.Old(() => _count);

        _count++;

        // Postcondition: Count increased by exactly 1
        Contract.Ensure("Count incremented", () => _count == oldCount + 1);
    }

    public void Add(int value)
    {
        Contract.Require("Value must be positive", () => value > 0);

        var oldCount = Contract.Old(() => _count);

        _count += value;

        Contract.Ensure("Count increased correctly", () => _count == oldCount + value);
    }

    public void Reset()
    {
        var oldCount = Contract.Old(() => _count);

        _count = 0;

        // Postcondition: Count is now zero (and changed)
        Contract.Ensure("Count is zero", () => _count == 0);
        Contract.Ensure("Count changed", () => _count != oldCount || oldCount == 0);
    }
}
```

### Capturing Complex Objects

Using `Old<T>()` with objects (deep copy via JSON serialization).

```csharp
using uContract;

public class ShoppingCart
{
    private List<CartItem> _items = new();
    private decimal _total;

    public void AddItem(string productName, decimal price, int quantity)
    {
        Contract.RequireNotEmpty("Product name", productName);
        Contract.Require("Price positive", () => price > 0);
        Contract.Require("Quantity positive", () => quantity > 0);

        // Capture state BEFORE modification (deep copy)
        var oldTotal = Contract.Old(() => _total);
        var oldItemCount = Contract.Old(() => _items.Count);
        var oldItems = Contract.Old(() => _items);  // Deep copy of list

        var item = new CartItem(productName, price, quantity);
        _items.Add(item);
        _total += price * quantity;

        // Postconditions: Verify state changes
        Contract.Ensure("Item added", () => _items.Count == oldItemCount + 1);
        Contract.Ensure("Total increased", () => _total == oldTotal + (price * quantity));
        Contract.Ensure("Old items unchanged", () => oldItems.Count == oldItemCount);
    }

    public void RemoveItem(int index)
    {
        Contract.Require("Valid index", () => index >= 0 && index < _items.Count);

        var oldItemCount = Contract.Old(() => _items.Count);
        var removedItem = Contract.Old(() => _items[index]);  // Capture item before removal
        var oldTotal = Contract.Old(() => _total);

        var item = _items[index];
        _items.RemoveAt(index);
        _total -= item.Price * item.Quantity;

        Contract.Ensure("Item removed", () => _items.Count == oldItemCount - 1);
        Contract.Ensure("Total decreased", () => _total == oldTotal - (removedItem.Price * removedItem.Quantity));
    }
}

public record CartItem(string ProductName, decimal Price, int Quantity);
```

### Field Assignment Validation

Using `EnsureAssignable<T>()` to validate which fields changed.

```csharp
using uContract;

public class User
{
    private string _email;
    private string _name;
    private DateTime _createdAt;
    private DateTime _lastModified;
    private int _loginCount;

    public User(string email, string name)
    {
        Contract.RequireNotEmpty("Email", email);
        Contract.RequireNotEmpty("Name", name);

        _email = email;
        _name = name;
        _createdAt = DateTime.UtcNow;
        _lastModified = DateTime.UtcNow;
        _loginCount = 0;
    }

    public void ChangeEmail(string newEmail)
    {
        Contract.RequireNotEmpty("Email", newEmail);
        Contract.Require("Valid email format", () => newEmail.Contains("@"));

        var oldState = Contract.Old(() => this);

        _email = newEmail;
        _lastModified = DateTime.UtcNow;

        // Only email and lastModified should change (not name, createdAt, loginCount)
        Contract.EnsureAssignable(this, oldState,
            nameof(_email),
            nameof(_lastModified));
    }

    public void ChangeName(string newName)
    {
        Contract.RequireNotEmpty("Name", newName);

        var oldState = Contract.Old(() => this);

        _name = newName;
        _lastModified = DateTime.UtcNow;

        // Only name and lastModified should change
        Contract.EnsureAssignable(this, oldState,
            nameof(_name),
            nameof(_lastModified));
    }

    public void RecordLogin()
    {
        var oldState = Contract.Old(() => this);

        _loginCount++;
        _lastModified = DateTime.UtcNow;

        // Only loginCount and lastModified should change
        Contract.EnsureAssignable(this, oldState,
            nameof(_loginCount),
            nameof(_lastModified));
    }
}
```

**Using Regex Patterns:**

```csharp
public class AuditableEntity
{
    private string _data;
    private DateTime _createdAt;
    private DateTime _modifiedAt;
    private string _createdBy;
    private string _modifiedBy;

    public void UpdateData(string newData, string modifiedBy)
    {
        var oldState = Contract.Old(() => this);

        _data = newData;
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = modifiedBy;

        // Allow changes to _data and any field containing "modified"
        Contract.EnsureAssignable(this, oldState,
            nameof(_data),
            ".*[Mm]odified.*");  // Regex: any field with "modified" in name
    }
}
```

---

## Domain-Driven Design Patterns

> 🏗️ **Complete DDD Integration Guide**: [DDD_INTEGRATION_GUIDE.md](../DDD_INTEGRATION_GUIDE.md) (1,300+ lines)

This section provides **quick DDD examples** to get started. For comprehensive DDD integration including theory, patterns, best practices, and real-world examples, see the complete guide.

### Quick Aggregate Root Example

Demonstrates the core DDD pattern: aggregate root with invariants.

```csharp
using uContract;

public class BankAccount  // Aggregate Root
{
    private decimal _balance;
    private readonly string _owner;

    public BankAccount(string owner, decimal initialBalance)
    {
        // Preconditions: Validate constructor inputs
        Contract.RequireNotEmpty("Owner", owner);
        Contract.Require("Balance >= 0", () => initialBalance >= 0);

        _owner = owner;
        _balance = initialBalance;

        CheckInvariant();
    }

    public void Withdraw(decimal amount)
    {
        // Preconditions: Validate command inputs
        Contract.Require("Amount > 0", () => amount > 0);
        Contract.Require("Sufficient funds", () => amount <= _balance);

        // Capture old state for postcondition
        var oldBalance = Contract.Old(() => _balance);

        // Execute command
        _balance -= amount;

        // Postcondition: Verify state change
        Contract.Ensure("Balance decreased", () => _balance == oldBalance - amount);

        // Invariant: Check aggregate consistency
        CheckInvariant();
    }

    private void CheckInvariant()
    {
        // Business rules that ALWAYS hold
        Contract.Invariant("Balance >= 0", () => _balance >= 0);
        Contract.InvariantNotNull("Owner exists", _owner);
    }
}
```

**Key DDD Concepts Demonstrated**:
- ✅ **Preconditions**: Validate commands at aggregate boundary
- ✅ **State Capture**: Use `Old<T>()` for before/after comparison
- ✅ **Postconditions**: Verify state transitions
- ✅ **Invariants**: Enforce aggregate consistency after every modification

### More DDD Patterns

The complete DDD guide includes:

- **Aggregate Roots** (3 patterns)
  - [Basic Aggregate Root](../DDD_INTEGRATION_GUIDE.md#basic-aggregate-root) - Fundamental pattern
  - [Invariant Checking Pattern](../DDD_INTEGRATION_GUIDE.md#invariant-checking-pattern) - When and how to check

- **Value Objects** (3 patterns)
  - [Immutable Value Object](../DDD_INTEGRATION_GUIDE.md#value-objects) - Money, Email, etc.
  - [Validation Rules](../DDD_INTEGRATION_GUIDE.md#value-objects) - Constructor validation
  - [Equality Semantics](../DDD_INTEGRATION_GUIDE.md#value-objects) - Value-based equality

- **Domain Services**
  - [Cross-Aggregate Operations](../DDD_INTEGRATION_GUIDE.md#domain-services) - Transfer service example
  - [Business Rules](../DDD_INTEGRATION_GUIDE.md#domain-services) - Complex domain logic

- **Additional Patterns**
  - [Entities](../DDD_INTEGRATION_GUIDE.md#entities) - Identity-based objects
  - [Repositories](../DDD_INTEGRATION_GUIDE.md#repositories) - Aggregate persistence
  - [Domain Events](../DDD_INTEGRATION_GUIDE.md#domain-events) - Event-driven architecture
  - [Specifications](../DDD_INTEGRATION_GUIDE.md#specifications) - Business rule objects

- **Best Practices**
  - [When to Use Contracts](../DDD_INTEGRATION_GUIDE.md#when-to-use-contracts-in-ddd) - Decision guide
  - [Common Patterns](../DDD_INTEGRATION_GUIDE.md#common-patterns) - Proven solutions
  - [Anti-Patterns to Avoid](../DDD_INTEGRATION_GUIDE.md#anti-patterns-to-avoid) - Common mistakes
  - [Real-World E-Commerce Example](../DDD_INTEGRATION_GUIDE.md#real-world-example-e-commerce-domain) - Complete implementation

---

## Advanced Patterns

### Method Chaining

Using `EnsureResult<T>()` for fluent APIs.

```csharp
using uContract;

public class QueryBuilder
{
    private string _table = "";
    private readonly List<string> _conditions = new();
    private int _limit = 100;

    public QueryBuilder From(string table)
    {
        Contract.RequireNotEmpty("Table name", table);

        _table = table;

        return Contract.EnsureResult("Table set", this, q => !string.IsNullOrEmpty(q._table));
    }

    public QueryBuilder Where(string condition)
    {
        Contract.RequireNotEmpty("Condition", condition);

        _conditions.Add(condition);

        return Contract.EnsureResult("Condition added", this, q => q._conditions.Count > 0);
    }

    public QueryBuilder Limit(int limit)
    {
        Contract.Require("Limit must be positive", () => limit > 0);

        _limit = limit;

        return Contract.EnsureResult("Limit set", this, q => q._limit > 0);
    }

    public string Build()
    {
        Contract.Require("Table must be set", () => !string.IsNullOrEmpty(_table));

        var query = $"SELECT * FROM {_table}";

        if (_conditions.Count > 0)
        {
            query += $" WHERE {string.Join(" AND ", _conditions)}";
        }

        query += $" LIMIT {_limit}";

        return Contract.EnsureResult("Query built", query, q => !string.IsNullOrEmpty(q));
    }
}

// Usage
var query = new QueryBuilder()
    .From("users")
    .Where("age > 18")
    .Where("status = 'active'")
    .Limit(50)
    .Build();
```

### Immutable Collections

Ensuring methods return immutable collections.

```csharp
using uContract;
using System.Collections.Immutable;

public class UserRepository
{
    private readonly List<User> _users = new();

    public ImmutableList<User> GetAllUsers()
    {
        var users = _users.ToImmutableList();

        return Contract.EnsureImmutableCollection(users);
    }

    public ImmutableList<User> GetActiveUsers()
    {
        var activeUsers = _users
            .Where(u => u.IsActive)
            .ToImmutableList();

        return Contract.EnsureImmutableCollection(activeUsers);
    }

    public ImmutableHashSet<string> GetUserEmails()
    {
        var emails = _users
            .Select(u => u.Email)
            .ToImmutableHashSet();

        return Contract.EnsureImmutableCollection(emails);
    }

    // Verify immutability at runtime
    public void VerifyImmutability()
    {
        var users = GetAllUsers();

        bool isImmutable = Contract.CheckUnsupportedOperation(() =>
        {
            var list = (IList<User>)users;
            list.Add(new User("test@example.com", "Test"));
        });

        Contract.Check("Collection is immutable", () => isImmutable);
    }
}
```

### Logical Operators

Using `Imply()` and `IfAndOnlyIf()` for complex business rules.

```csharp
using uContract;

public class Subscription
{
    private bool _isPremium;
    private DateTime? _expirationDate;
    private SubscriptionStatus _status;
    private readonly List<Feature> _features = new();

    public Subscription(bool isPremium)
    {
        _isPremium = isPremium;
        _status = SubscriptionStatus.Active;

        if (isPremium)
        {
            _expirationDate = DateTime.UtcNow.AddYears(1);
            _features.Add(Feature.AdvancedReporting);
            _features.Add(Feature.PrioritySupport);
        }

        CheckInvariant();
    }

    private void CheckInvariant()
    {
        // Rule: If premium, then must have expiration date
        Contract.Invariant("Premium subscriptions have expiration",
            () => Contract.Imply(() => _isPremium, () => _expirationDate.HasValue));

        // Rule: If premium, then must have premium features
        Contract.Invariant("Premium subscriptions have features",
            () => Contract.Imply(() => _isPremium, () => _features.Count > 0));

        // Rule: Subscription is active if and only if not expired
        Contract.Invariant("Active subscription is not expired",
            () => Contract.IfAndOnlyIf(
                () => _status == SubscriptionStatus.Active,
                () => !_expirationDate.HasValue || _expirationDate.Value > DateTime.UtcNow));

        // Rule: Premium if and only if has premium features
        Contract.Invariant("Premium means has premium features",
            () => Contract.IfAndOnlyIf(
                () => _isPremium,
                () => _features.Any(f => f == Feature.AdvancedReporting || f == Feature.PrioritySupport)));
    }
}

public enum SubscriptionStatus { Active, Expired, Cancelled }
public enum Feature { BasicReporting, AdvancedReporting, PrioritySupport }
```

### Early Return Pattern

Using `Ignore()` for DDD early return pattern.

```csharp
using uContract;

public class Product
{
    private string _name;
    private decimal _price;
    private int _stockQuantity;

    public void UpdatePrice(decimal newPrice)
    {
        Contract.Require("Price must be positive", () => newPrice > 0);

        // Early return if price unchanged (avoid unnecessary work)
        if (Contract.Ignore("Price unchanged", () => _price == newPrice))
            return;

        var oldPrice = _price;
        _price = newPrice;

        // Raise domain event only if price actually changed
        RaiseDomainEvent(new PriceChangedEvent(oldPrice, newPrice));

        Contract.Ensure("Price updated", () => _price == newPrice);
    }

    public void AdjustStock(int quantity)
    {
        // Early return if no adjustment needed
        if (Contract.Ignore("No adjustment needed", () => quantity == 0))
            return;

        Contract.Require("Sufficient stock for negative adjustment",
            () => quantity >= 0 || _stockQuantity >= Math.Abs(quantity));

        var oldQuantity = _stockQuantity;
        _stockQuantity += quantity;

        RaiseDomainEvent(new StockAdjustedEvent(oldQuantity, _stockQuantity));

        Contract.Ensure("Stock adjusted", () => _stockQuantity == oldQuantity + quantity);
    }

    private void RaiseDomainEvent(IDomainEvent evt)
    {
        // Event raising logic
    }
}

public record PriceChangedEvent(decimal OldPrice, decimal NewPrice) : IDomainEvent;
public record StockAdjustedEvent(int OldQuantity, int NewQuantity) : IDomainEvent;
```

---

## Real-World Scenarios

### Banking System

Complete banking system example.

```csharp
using uContract;
using System.Collections.Immutable;

public class BankingSystem
{
    private readonly Dictionary<string, BankAccount> _accounts = new();

    public string CreateAccount(string owner, decimal initialDeposit)
    {
        Contract.RequireNotEmpty("Owner name", owner);
        Contract.Require("Initial deposit non-negative", () => initialDeposit >= 0);

        var accountId = Guid.NewGuid().ToString();
        var account = new BankAccount(accountId, owner, initialDeposit);

        _accounts[accountId] = account;

        Contract.EnsureNotNull("Account created", account);
        Contract.Ensure("Account added to system", () => _accounts.ContainsKey(accountId));

        return accountId;
    }

    public void Transfer(string fromAccountId, string toAccountId, decimal amount)
    {
        Contract.RequireNotEmpty("From account", fromAccountId);
        Contract.RequireNotEmpty("To account", toAccountId);
        Contract.Require("Amount positive", () => amount > 0);
        Contract.Require("Different accounts", () => fromAccountId != toAccountId);

        var fromAccount = _accounts[fromAccountId];
        var toAccount = _accounts[toAccountId];

        Contract.Check("From account exists", () => fromAccount != null);
        Contract.Check("To account exists", () => toAccount != null);

        var oldFromBalance = Contract.Old(() => fromAccount.GetBalance());
        var oldToBalance = Contract.Old(() => toAccount.GetBalance());
        var oldTotalMoney = oldFromBalance + oldToBalance;

        fromAccount.Withdraw(amount);
        toAccount.Deposit(amount);

        // Conservation of money
        Contract.Ensure("Money conserved",
            () => fromAccount.GetBalance() + toAccount.GetBalance() == oldTotalMoney);
    }

    public ImmutableList<string> GetAllAccountIds()
    {
        return Contract.EnsureImmutableCollection(_accounts.Keys.ToImmutableList());
    }
}
```

### E-Commerce Order Processing

E-commerce order workflow.

```csharp
using uContract;

public class OrderProcessor
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IInventoryService _inventoryService;
    private readonly IShippingService _shippingService;

    public OrderProcessor(
        IPaymentGateway paymentGateway,
        IInventoryService inventoryService,
        IShippingService shippingService)
    {
        Contract.RequireNotNull("Payment gateway", paymentGateway);
        Contract.RequireNotNull("Inventory service", inventoryService);
        Contract.RequireNotNull("Shipping service", shippingService);

        _paymentGateway = paymentGateway;
        _inventoryService = inventoryService;
        _shippingService = shippingService;
    }

    public void ProcessOrder(Order order)
    {
        Contract.RequireNotNull("Order", order);
        Contract.Require("Order must be submitted", () => order.Status == OrderStatus.Submitted);
        Contract.Require("Order must have items", () => order.Items.Count > 0);

        // Step 1: Reserve inventory
        foreach (var item in order.Items)
        {
            bool reserved = _inventoryService.ReserveStock(item.ProductId, item.Quantity);
            Contract.Check("Stock reserved", () => reserved);
        }

        // Step 2: Process payment
        var paymentResult = _paymentGateway.Charge(order.Customer.PaymentMethod, order.Total);
        Contract.Check("Payment successful", () => paymentResult.Success);

        order.MarkAsPaid();
        Contract.Ensure("Order is paid", () => order.Status == OrderStatus.Paid);

        // Step 3: Arrange shipping
        var trackingNumber = _shippingService.CreateShipment(order);
        Contract.Check("Tracking number received", () => !string.IsNullOrEmpty(trackingNumber));

        order.MarkAsShipped(trackingNumber);

        Contract.Ensure("Order is shipped", () => order.Status == OrderStatus.Shipped);
        Contract.Ensure("Order has tracking number",
            () => !string.IsNullOrEmpty(order.TrackingNumber));
    }
}

public interface IPaymentGateway
{
    PaymentResult Charge(string paymentMethod, decimal amount);
}

public interface IInventoryService
{
    bool ReserveStock(string productId, int quantity);
}

public interface IShippingService
{
    string CreateShipment(Order order);
}

public record PaymentResult(bool Success, string TransactionId);
```

### User Management

User registration and authentication.

```csharp
using uContract;
using System.Security.Cryptography;
using System.Text;

public class UserManager
{
    private readonly Dictionary<string, User> _users = new();
    private readonly HashSet<string> _usedEmails = new();

    public User RegisterUser(string email, string password, string name)
    {
        Contract.RequireNotEmpty("Email", email);
        Contract.RequireNotEmpty("Password", password);
        Contract.RequireNotEmpty("Name", name);
        Contract.Require("Valid email", () => email.Contains("@"));
        Contract.Require("Password long enough", () => password.Length >= 8);

        Contract.Require("Email not already used", () => !_usedEmails.Contains(email));

        var userId = Guid.NewGuid().ToString();
        var passwordHash = HashPassword(password);
        var user = new User(userId, email, passwordHash, name);

        _users[userId] = user;
        _usedEmails.Add(email);

        Contract.Ensure("User registered", () => _users.ContainsKey(userId));
        Contract.Ensure("Email marked as used", () => _usedEmails.Contains(email));
        Contract.EnsureNotNull("User created", user);

        return user;
    }

    public User? Authenticate(string email, string password)
    {
        Contract.RequireNotEmpty("Email", email);
        Contract.RequireNotEmpty("Password", password);

        var user = _users.Values.FirstOrDefault(u => u.Email == email);

        if (user == null)
            return null;

        var passwordHash = HashPassword(password);

        if (user.PasswordHash != passwordHash)
            return null;

        user.RecordLogin();

        Contract.Ensure("Login recorded", () => user.LastLoginAt > DateTime.UtcNow.AddMinutes(-1));

        return user;
    }

    public void ChangePassword(string userId, string oldPassword, string newPassword)
    {
        Contract.RequireNotEmpty("User ID", userId);
        Contract.RequireNotEmpty("Old password", oldPassword);
        Contract.RequireNotEmpty("New password", newPassword);
        Contract.Require("New password long enough", () => newPassword.Length >= 8);
        Contract.Require("Password changed", () => oldPassword != newPassword);

        Contract.Require("User exists", () => _users.ContainsKey(userId));
        var user = _users[userId];

        var oldPasswordHash = HashPassword(oldPassword);
        Contract.Require("Old password correct", () => user.PasswordHash == oldPasswordHash);

        var oldState = Contract.Old(() => user);
        var newPasswordHash = HashPassword(newPassword);
        user.ChangePassword(newPasswordHash);

        Contract.Ensure("Password changed", () => user.PasswordHash != oldPasswordHash);
        Contract.EnsureAssignable(user, oldState, nameof(User.PasswordHash));
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
```

### Inventory System

Inventory management with stock tracking.

```csharp
using uContract;

public class InventoryItem
{
    private string _productId;
    private string _productName;
    private int _stockQuantity;
    private int _reservedQuantity;
    private int _reorderThreshold;

    public InventoryItem(string productId, string productName, int initialStock, int reorderThreshold)
    {
        Contract.RequireNotEmpty("Product ID", productId);
        Contract.RequireNotEmpty("Product name", productName);
        Contract.Require("Initial stock non-negative", () => initialStock >= 0);
        Contract.Require("Reorder threshold non-negative", () => reorderThreshold >= 0);

        _productId = productId;
        _productName = productName;
        _stockQuantity = initialStock;
        _reservedQuantity = 0;
        _reorderThreshold = reorderThreshold;

        CheckInvariant();
    }

    public bool ReserveStock(int quantity)
    {
        Contract.Require("Quantity positive", () => quantity > 0);

        // Early return if insufficient stock
        if (Contract.Ignore("Insufficient stock", () => GetAvailableStock() < quantity))
            return false;

        var oldReserved = Contract.Old(() => _reservedQuantity);
        var oldAvailable = Contract.Old(() => GetAvailableStock());

        _reservedQuantity += quantity;

        Contract.Ensure("Stock reserved", () => _reservedQuantity == oldReserved + quantity);
        Contract.Ensure("Available decreased", () => GetAvailableStock() == oldAvailable - quantity);

        CheckInvariant();
        return true;
    }

    public void CommitReservation(int quantity)
    {
        Contract.Require("Quantity positive", () => quantity > 0);
        Contract.Require("Sufficient reserved", () => quantity <= _reservedQuantity);

        var oldStock = Contract.Old(() => _stockQuantity);
        var oldReserved = Contract.Old(() => _reservedQuantity);

        _stockQuantity -= quantity;
        _reservedQuantity -= quantity;

        Contract.Ensure("Stock decreased", () => _stockQuantity == oldStock - quantity);
        Contract.Ensure("Reserved decreased", () => _reservedQuantity == oldReserved - quantity);

        CheckInvariant();
    }

    public void Restock(int quantity)
    {
        Contract.Require("Quantity positive", () => quantity > 0);

        var oldStock = Contract.Old(() => _stockQuantity);

        _stockQuantity += quantity;

        Contract.Ensure("Stock increased", () => _stockQuantity == oldStock + quantity);

        CheckInvariant();
    }

    public int GetAvailableStock()
    {
        return _stockQuantity - _reservedQuantity;
    }

    public bool NeedsReorder()
    {
        return GetAvailableStock() <= _reorderThreshold;
    }

    private void CheckInvariant()
    {
        Contract.Invariant("Stock non-negative", () => _stockQuantity >= 0);
        Contract.Invariant("Reserved non-negative", () => _reservedQuantity >= 0);
        Contract.Invariant("Reserved not exceed stock", () => _reservedQuantity <= _stockQuantity);
        Contract.Invariant("Available is correct", () => GetAvailableStock() == _stockQuantity - _reservedQuantity);
    }
}
```

---

## Configuration Examples

### Setting Environment Variables

**Windows (PowerShell):**
```powershell
# Enable all contracts
$env:DBC="on"
$env:DBC_PRE="on"
$env:DBC_POST="on"
$env:DBC_INV="on"
$env:DBC_CHECK="on"

# Disable postconditions only (e.g., for performance)
$env:DBC_POST="off"

# Disable all contracts
$env:DBC="off"
```

**Linux/macOS:**
```bash
# Enable all contracts
export DBC=on
export DBC_PRE=on
export DBC_POST=on
export DBC_INV=on
export DBC_CHECK=on

# Disable postconditions only
export DBC_POST=off

# Disable all contracts
export DBC=off
```

**Docker (docker-compose.yml):**
```yaml
services:
  app:
    image: myapp:latest
    environment:
      - DBC=on
      - DBC_PRE=on
      - DBC_POST=off  # Disable for performance
      - DBC_INV=on
      - DBC_CHECK=on
```

**ASP.NET Core (launchSettings.json):**
```json
{
  "profiles": {
    "Development": {
      "environmentVariables": {
        "DBC": "on",
        "DBC_PRE": "on",
        "DBC_POST": "on",
        "DBC_INV": "on",
        "DBC_CHECK": "on"
      }
    },
    "Production": {
      "environmentVariables": {
        "DBC": "off"
      }
    }
  }
}
```

---

## Testing with Contracts

### Unit Testing Contracts

```csharp
using Xunit;
using uContract;
using uContract.Exceptions;

public class BankAccountTests
{
    [Fact]
    public void Withdraw_ShouldThrow_WhenAmountExceedsBalance()
    {
        // Arrange
        var account = new BankAccount("123", "John Doe", 100);

        // Act & Assert
        var exception = Assert.Throws<PreconditionViolationException>(() =>
            account.Withdraw(150));

        Assert.Contains("Sufficient funds", exception.Message);
    }

    [Fact]
    public void Withdraw_ShouldDecreaseBalance_WhenAmountValid()
    {
        // Arrange
        var account = new BankAccount("123", "John Doe", 100);

        // Act
        account.Withdraw(30);

        // Assert
        Assert.Equal(70, account.GetBalance());
    }

    [Fact]
    public void Close_ShouldThrow_WhenBalanceNotZero()
    {
        // Arrange
        var account = new BankAccount("123", "John Doe", 100);

        // Act & Assert
        var exception = Assert.Throws<PreconditionViolationException>(() =>
            account.Close());

        Assert.Contains("Cannot close account with positive balance", exception.Message);
    }
}
```

### Integration Testing with Contracts Disabled

```csharp
public class OrderProcessorIntegrationTests : IDisposable
{
    public OrderProcessorIntegrationTests()
    {
        // Disable contracts for integration tests (performance)
        Environment.SetEnvironmentVariable("DBC", "off");
    }

    [Fact]
    public void ProcessOrder_ShouldCompleteSuccessfully()
    {
        // Integration test logic without contract overhead
    }

    public void Dispose()
    {
        // Re-enable contracts after tests
        Environment.SetEnvironmentVariable("DBC", "on");
    }
}
```

---

## See Also

- [API Reference](API_REFERENCE.md) - Complete API documentation
- [DDD Integration Guide](../DDD_INTEGRATION_GUIDE.md) - DDD patterns and best practices
- [Architecture Decision Records](../adr/) - Design decisions
- [README.md](../../README.md) - Project overview

---

