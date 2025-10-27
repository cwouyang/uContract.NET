# uContract.NET - Domain-Driven Design Integration Guide

Complete guide for integrating uContract.NET with Domain-Driven Design (DDD).

> **Note**: Examples use fictional domain classes for illustration.
> For compilable, tested code, see the [test suite](../../tests/uContract.Tests/).

---

## Table of Contents

- [Introduction](#introduction)
- [DDD Core Concepts](#ddd-core-concepts)
- [When to Use Contracts in DDD](#when-to-use-contracts-in-ddd)
- [Aggregate Roots](#aggregate-roots)
  - [Basic Aggregate Root](#basic-aggregate-root)
  - [Invariant Checking Pattern](#invariant-checking-pattern)
- [Value Objects](#value-objects)
- [Entities](#entities)
- [Domain Services](#domain-services)
- [Repositories](#repositories)
- [Domain Events](#domain-events)
- [Specifications](#specifications)
- [Best Practices](#best-practices)
- [Common Patterns](#common-patterns)
- [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
- [Performance Considerations](#performance-considerations)
- [Testing DDD with Contracts](#testing-ddd-with-contracts)
- [Real-World Example: E-Commerce Domain](#real-world-example-e-commerce-domain)

---

## Introduction

uContract.NET is designed for Domain-Driven Design. This guide demonstrates how to leverage Design by Contract (DBC) to:

- ✅ Enforce **business rules** as invariants
- ✅ Validate **aggregate consistency**
- ✅ Document **domain constraints** explicitly
- ✅ Prevent **invalid state transitions**
- ✅ Support **fail-fast** error detection
- ✅ Enable **self-documenting** domain models

### Why DBC + DDD?

| Challenge | DBC Solution |
|-----------|--------------|
| Complex business rules | Express as declarative contracts |
| Implicit invariants | Make invariants explicit and executable |
| Hidden preconditions | Document and enforce at method entry |
| State corruption | Validate state after every modification |
| Unclear postconditions | Express guarantees explicitly |
| Difficult testing | Contracts serve as executable specifications |

### Theoretical Foundation

uContract.NET implements **root contracting**, based on peer-reviewed research:

📖 **Chen, C.-T.; Yen, Y.-C.; Hu, Y.-H.; Cheng, Y.C.** (2025). Root Contracting: A Novel Method and Utility for Implementing Design by Contract in Domain-Driven Design with Event Sourcing. *Electronics*, 14(21), 4205. https://doi.org/10.3390/electronics14214205

**Read the paper**:
- 🌐 [Online version](https://www.mdpi.com/2079-9292/14/21/4205) - Open access, read in browser
- 📄 [Local PDF](paper/Root%20Contracting%20-%20A%20Novel%20Method%20and%20Utility%20for%20Implementing%20Design%20by%20Contract%20in%20Domain-Driven%20Design%20with%20Event%20Sourcing.pdf) - Offline reading

---

## DDD Core Concepts

### Design by Contract in DDD Context

| DDD Concept | DBC Mapping |
|-------------|-------------|
| **Aggregate Root** | Class with invariants checked after every public method |
| **Value Object** | Immutable object with preconditions in constructor |
| **Entity** | Object with identity and lifecycle invariants |
| **Domain Service** | Methods with preconditions and postconditions |
| **Repository** | Interface with contracts on query/persistence methods |
| **Domain Event** | Postcondition: event raised after state change |
| **Specification** | Precondition: specification satisfied before operation |

### Contract Types in DDD

```csharp
// Preconditions: Validate commands and caller state
public void Execute(Command command)
{
    Contract.RequireNotNull("Command", command);
    Contract.Require("User authorized", () => _currentUser.CanExecute(command));
    // ...
}

// Postconditions: Verify state changes and events
public void PlaceOrder(Order order)
{
    // ...
    Contract.Ensure("Order placed", () => order.Status == OrderStatus.Placed);
    Contract.Ensure("Event raised", () => _events.Any(e => e is OrderPlacedEvent));
}

// Invariants: Enforce aggregate consistency
private void CheckInvariant()
{
    Contract.Invariant("Balance non-negative", () => _balance >= 0);
    Contract.Invariant("Owner exists", () => _owner != null);
}
```

---

## When to Use Contracts in DDD

### Always Use Contracts For:

✅ **Aggregate Root public methods**
- Validate all inputs (preconditions)
- Check invariants after modifications
- Verify state transitions (postconditions)

✅ **Value Object constructors**
- Validate all constructor parameters
- Ensure immutability constraints
- Check value ranges

✅ **Domain Service operations**
- Validate business rules
- Verify cross-aggregate consistency
- Document domain constraints

✅ **Critical business rules**
- Non-negative balances
- Valid state transitions
- Required fields

### Consider Using Contracts For:

⚠️ **Entity methods**
- Use when lifecycle constraints are complex
- Skip for simple CRUD operations

⚠️ **Domain Event handlers**
- Use when side effects must be verified
- Skip for simple logging

### Don't Use Contracts For:

❌ **Infrastructure concerns**
- Database connections
- HTTP requests
- File I/O

❌ **Simple getters/setters**
- Unless they enforce business rules

❌ **Private helper methods**
- Use for public API only (internal consistency)

---

## Aggregate Roots

Aggregate Roots are the core of DDD. They maintain consistency boundaries and enforce business rules.

### Basic Aggregate Root

```csharp
using uContract;

public class BankAccount  // Aggregate Root
{
    // Identity
    private readonly string _accountId;

    // State
    private string _owner;
    private decimal _balance;
    private AccountStatus _status;
    private readonly List<Transaction> _transactions;

    // Constructor: Initialize with invariants
    public BankAccount(string accountId, string owner, decimal initialBalance)
    {
        // Preconditions: Validate inputs
        Contract.RequireNotEmpty("Account ID", accountId);
        Contract.RequireNotEmpty("Owner", owner);
        Contract.Require("Initial balance non-negative", () => initialBalance >= 0);

        _accountId = accountId;
        _owner = owner;
        _balance = initialBalance;
        _status = AccountStatus.Active;
        _transactions = new List<Transaction>();

        if (initialBalance > 0)
        {
            _transactions.Add(new Transaction(TransactionType.Deposit, initialBalance));
        }

        // Postcondition: Invariants hold
        CheckInvariant();
    }

    // Command: Deposit money
    public void Deposit(decimal amount)
    {
        // Preconditions: Validate command
        Contract.Require("Amount positive", () => amount > 0);
        Contract.Require("Account active", () => _status == AccountStatus.Active);

        // Capture old state
        var oldBalance = Contract.Old(() => _balance);

        // Execute command
        _balance += amount;
        _transactions.Add(new Transaction(TransactionType.Deposit, amount));

        // Postconditions: Verify state change
        Contract.Ensure("Balance increased", () => _balance == oldBalance + amount);
        Contract.Ensure("Transaction recorded", () => _transactions.Count > 0);

        // Invariants: Check consistency
        CheckInvariant();
    }

    // Command: Withdraw money
    public void Withdraw(decimal amount)
    {
        Contract.Require("Amount positive", () => amount > 0);
        Contract.Require("Sufficient funds", () => amount <= _balance);
        Contract.Require("Account active", () => _status == AccountStatus.Active);

        var oldBalance = Contract.Old(() => _balance);

        _balance -= amount;
        _transactions.Add(new Transaction(TransactionType.Withdrawal, amount));

        Contract.Ensure("Balance decreased", () => _balance == oldBalance - amount);
        Contract.Ensure("Balance non-negative", () => _balance >= 0);

        CheckInvariant();
    }

    // Command: Close account
    public void Close()
    {
        Contract.Require("Balance must be zero", () => _balance == 0);
        Contract.Require("Account not already closed", () => _status != AccountStatus.Closed);

        _status = AccountStatus.Closed;

        Contract.Ensure("Account closed", () => _status == AccountStatus.Closed);
        CheckInvariant();
    }

    // Query: Get balance (no contracts needed for simple queries)
    public decimal GetBalance() => _balance;

    // Invariants: Aggregate consistency rules
    private void CheckInvariant()
    {
        // Business rules that ALWAYS hold
        Contract.Invariant("Balance non-negative", () => _balance >= 0);
        Contract.InvariantNotNull("Account ID", _accountId);
        Contract.InvariantNotNull("Owner", _owner);
        Contract.Invariant("Transactions list exists", () => _transactions != null);

        // Complex business rules
        Contract.Invariant("Closed accounts have zero balance",
            () => Contract.Imply(() => _status == AccountStatus.Closed, () => _balance == 0));
    }
}

public enum AccountStatus { Active, Closed }
public enum TransactionType { Deposit, Withdrawal }
public record Transaction(TransactionType Type, decimal Amount, DateTime Timestamp = default)
{
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}
```

### Invariant Checking Pattern

**Pattern: Check invariants at aggregate boundaries**

```csharp
public class Order
{
    public Order(string orderId, Customer customer)
    {
        // ... initialization ...
        CheckInvariant();  // ✅ Check after construction
    }

    public void AddItem(OrderItem item)
    {
        // ... add item ...
        CheckInvariant();  // ✅ Check after modification
    }

    public void Submit()
    {
        // ... submit order ...
        CheckInvariant();  // ✅ Check after state transition
    }

    private void CheckInvariant()
    {
        // All business rules that must ALWAYS hold
        Contract.Invariant("Order has customer", () => _customer != null);
        Contract.Invariant("Total matches items", () => _total == _items.Sum(i => i.Total));
        Contract.Invariant("Submitted orders have items",
            () => Contract.Imply(() => _status != OrderStatus.Draft, () => _items.Count > 0));
    }
}
```

**When to Check Invariants:**
- ✅ After constructor completion
- ✅ After every public method (commands)
- ✅ After state transitions
- ❌ NOT in the middle of a method
- ❌ NOT in private helper methods (only at boundaries)

---

## Value Objects

Value Objects are immutable and defined by their attributes. Contracts ensure they're always valid.

### Complete Value Object Example

```csharp
using uContract;

public sealed class Money  // Value Object
{
    public decimal Amount { get; }
    public string Currency { get; }

    // Constructor: Validate all inputs (preconditions)
    public Money(decimal amount, string currency)
    {
        // Preconditions: Validate inputs
        Contract.Require("Amount non-negative", () => amount >= 0);
        Contract.RequireNotEmpty("Currency", currency);
        Contract.Require("Currency 3 letters", () => currency.Length == 3);
        Contract.Require("Currency uppercase", () => currency == currency.ToUpper());

        Amount = amount;
        Currency = currency;

        // Postconditions: Check invariants immediately
        CheckInvariant();
    }

    // Operations return NEW value objects (immutability)
    public Money Add(Money other)
    {
        Contract.RequireNotNull("Other money", other);
        Contract.Require("Currency match", () => Currency == other.Currency);

        var result = new Money(Amount + other.Amount, Currency);

        Contract.Ensure("Result correct", () => result.Amount == Amount + other.Amount);
        Contract.Ensure("Currency preserved", () => result.Currency == Currency);

        return result;
    }

    public Money Subtract(Money other)
    {
        Contract.RequireNotNull("Other money", other);
        Contract.Require("Currency match", () => Currency == other.Currency);
        Contract.Require("Sufficient amount", () => Amount >= other.Amount);

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
    {
        Contract.Require("Factor non-negative", () => factor >= 0);

        return new Money(Amount * factor, Currency);
    }

    // Invariants: Value object consistency
    private void CheckInvariant()
    {
        Contract.Invariant("Amount non-negative", () => Amount >= 0);
        Contract.InvariantNotNull("Currency", Currency);
        Contract.Invariant("Currency 3 letters", () => Currency.Length == 3);
    }

    // Value equality
    public override bool Equals(object? obj) =>
        obj is Money other && Amount == other.Amount && Currency == other.Currency;

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public override string ToString() => $"{Amount:F2} {Currency}";

    // Static factory methods
    public static Money Zero(string currency)
    {
        Contract.RequireNotEmpty("Currency", currency);
        return new Money(0, currency.ToUpper());
    }

    public static Money FromDollars(decimal amount)
    {
        return new Money(amount, "USD");
    }
}
```

### Value Object Patterns

**Pattern 1: Parse with validation**

```csharp
public sealed class EmailAddress
{
    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
        CheckInvariant();
    }

    public static EmailAddress Parse(string input)
    {
        Contract.RequireNotEmpty("Email", input);
        Contract.Require("Valid format", () => input.Contains("@"));
        Contract.Require("Valid domain", () => input.Split('@')[1].Contains("."));

        return new EmailAddress(input.ToLower());
    }

    public static bool TryParse(string input, out EmailAddress? email)
    {
        email = null;

        if (string.IsNullOrEmpty(input) || !input.Contains("@"))
            return false;

        email = new EmailAddress(input.ToLower());
        return true;
    }

    private void CheckInvariant()
    {
        Contract.InvariantNotNull("Email value", Value);
        Contract.Invariant("Contains @", () => Value.Contains("@"));
    }
}
```

**Pattern 2: Defensive copying**

```csharp
public sealed class Address
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(string street, string city, string postalCode, string country)
    {
        Contract.RequireNotEmpty("Street", street);
        Contract.RequireNotEmpty("City", city);
        Contract.RequireNotEmpty("Postal code", postalCode);
        Contract.RequireNotEmpty("Country", country);

        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;

        CheckInvariant();
    }

    public Address WithStreet(string newStreet)
    {
        // Returns NEW instance (immutability)
        return new Address(newStreet, City, PostalCode, Country);
    }

    public Address WithCity(string newCity)
    {
        return new Address(Street, newCity, PostalCode, Country);
    }

    private void CheckInvariant()
    {
        Contract.InvariantNotNull("Street", Street);
        Contract.InvariantNotNull("City", City);
        Contract.InvariantNotNull("Postal code", PostalCode);
        Contract.InvariantNotNull("Country", Country);
    }
}
```

---

## Entities

Entities have identity and lifecycle. Use contracts to enforce lifecycle rules.

```csharp
using uContract;

public class User  // Entity (has identity)
{
    // Identity
    private readonly string _userId;

    // State
    private string _email;
    private string _name;
    private UserStatus _status;
    private DateTime _createdAt;
    private DateTime _lastModifiedAt;
    private int _failedLoginAttempts;

    public User(string userId, string email, string name)
    {
        Contract.RequireNotEmpty("User ID", userId);
        Contract.RequireNotEmpty("Email", email);
        Contract.RequireNotEmpty("Name", name);
        Contract.Require("Valid email", () => email.Contains("@"));

        _userId = userId;
        _email = email;
        _name = name;
        _status = UserStatus.Active;
        _createdAt = DateTime.UtcNow;
        _lastModifiedAt = DateTime.UtcNow;
        _failedLoginAttempts = 0;

        CheckInvariant();
    }

    // Command: Change email
    public void ChangeEmail(string newEmail)
    {
        Contract.RequireNotEmpty("Email", newEmail);
        Contract.Require("Valid email", () => newEmail.Contains("@"));
        Contract.Require("User active", () => _status == UserStatus.Active);

        // Early return pattern
        if (Contract.Ignore("Email unchanged", () => _email == newEmail))
            return;

        var oldState = Contract.Old(() => this);

        _email = newEmail;
        _lastModifiedAt = DateTime.UtcNow;

        // Only email and lastModified should change
        Contract.EnsureAssignable(this, oldState,
            nameof(_email),
            nameof(_lastModifiedAt));

        CheckInvariant();
    }

    // Command: Record failed login
    public void RecordFailedLogin()
    {
        Contract.Require("User not locked", () => _status != UserStatus.Locked);

        var oldAttempts = Contract.Old(() => _failedLoginAttempts);

        _failedLoginAttempts++;

        if (_failedLoginAttempts >= 5)
        {
            _status = UserStatus.Locked;
        }

        Contract.Ensure("Attempts incremented", () => _failedLoginAttempts == oldAttempts + 1);
        CheckInvariant();
    }

    // Command: Reset failed logins
    public void ResetFailedLogins()
    {
        _failedLoginAttempts = 0;

        if (_status == UserStatus.Locked)
        {
            _status = UserStatus.Active;
        }

        Contract.Ensure("Attempts reset", () => _failedLoginAttempts == 0);
        CheckInvariant();
    }

    // Invariants: Lifecycle rules
    private void CheckInvariant()
    {
        Contract.InvariantNotNull("User ID", _userId);
        Contract.InvariantNotNull("Email", _email);
        Contract.InvariantNotNull("Name", _name);
        Contract.Invariant("Created before modified",
            () => _createdAt <= _lastModifiedAt);
        Contract.Invariant("Failed attempts non-negative",
            () => _failedLoginAttempts >= 0);

        // Business rule: Locked if and only if 5+ failed attempts
        Contract.Invariant("Lock rule",
            () => Contract.IfAndOnlyIf(
                () => _status == UserStatus.Locked,
                () => _failedLoginAttempts >= 5));
    }
}

public enum UserStatus { Active, Locked, Deactivated }
```

---

## Domain Services

Domain Services orchestrate operations across aggregates. Use contracts to validate business rules.

```csharp
using uContract;

public class PaymentProcessingService  // Domain Service
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionLog _transactionLog;

    public PaymentProcessingService(
        IPaymentGateway paymentGateway,
        IAccountRepository accountRepository,
        ITransactionLog transactionLog)
    {
        Contract.RequireNotNull("Payment gateway", paymentGateway);
        Contract.RequireNotNull("Account repository", accountRepository);
        Contract.RequireNotNull("Transaction log", transactionLog);

        _paymentGateway = paymentGateway;
        _accountRepository = accountRepository;
        _transactionLog = transactionLog;
    }

    public PaymentResult ProcessPayment(string accountId, Money amount, string paymentMethod)
    {
        // Preconditions: Validate inputs
        Contract.RequireNotEmpty("Account ID", accountId);
        Contract.RequireNotNull("Amount", amount);
        Contract.RequireNotEmpty("Payment method", paymentMethod);
        Contract.Require("Amount positive", () => amount.Amount > 0);

        // Load aggregate
        var account = _accountRepository.GetById(accountId);
        Contract.Check("Account exists", () => account != null);
        Contract.Check("Account active", () => account!.Status == AccountStatus.Active);

        // Capture state
        var oldBalance = Contract.Old(() => account!.GetBalance());

        // Execute domain operation
        var gatewayResult = _paymentGateway.Charge(paymentMethod, amount);

        if (!gatewayResult.Success)
        {
            return PaymentResult.Failed(gatewayResult.ErrorMessage);
        }

        account!.Deposit(amount.Amount);
        _accountRepository.Save(account);
        _transactionLog.Log(accountId, amount, gatewayResult.TransactionId);

        // Postconditions: Verify operation
        Contract.Ensure("Balance increased",
            () => account.GetBalance() == oldBalance + amount.Amount);
        Contract.Ensure("Transaction logged",
            () => _transactionLog.GetLastTransaction(accountId) != null);

        return PaymentResult.Success(gatewayResult.TransactionId);
    }
}

public interface IPaymentGateway
{
    GatewayResult Charge(string paymentMethod, Money amount);
}

public interface IAccountRepository
{
    BankAccount? GetById(string accountId);
    void Save(BankAccount account);
}

public interface ITransactionLog
{
    void Log(string accountId, Money amount, string transactionId);
    TransactionRecord? GetLastTransaction(string accountId);
}

public record PaymentResult(bool Success, string? TransactionId, string? ErrorMessage)
{
    public static PaymentResult Success(string transactionId) =>
        new(true, transactionId, null);

    public static PaymentResult Failed(string errorMessage) =>
        new(false, null, errorMessage);
}

public record GatewayResult(bool Success, string? TransactionId, string? ErrorMessage);
public record TransactionRecord(string AccountId, Money Amount, string TransactionId, DateTime Timestamp);
```

---

## Repositories

Repositories manage aggregate persistence. Use contracts to ensure data integrity.

```csharp
using uContract;
using System.Collections.Immutable;

public interface IOrderRepository
{
    Order? GetById(string orderId);
    ImmutableList<Order> GetByCustomerId(string customerId);
    void Save(Order order);
    void Delete(string orderId);
}

public class OrderRepository : IOrderRepository
{
    private readonly Dictionary<string, Order> _orders = new();

    public Order? GetById(string orderId)
    {
        Contract.RequireNotEmpty("Order ID", orderId);

        return _orders.GetValueOrDefault(orderId);
    }

    public ImmutableList<Order> GetByCustomerId(string customerId)
    {
        Contract.RequireNotEmpty("Customer ID", customerId);

        var orders = _orders.Values
            .Where(o => o.CustomerId == customerId)
            .ToImmutableList();

        return Contract.EnsureImmutableCollection(orders);
    }

    public void Save(Order order)
    {
        Contract.RequireNotNull("Order", order);

        _orders[order.OrderId] = order;

        Contract.Ensure("Order saved", () => _orders.ContainsKey(order.OrderId));
    }

    public void Delete(string orderId)
    {
        Contract.RequireNotEmpty("Order ID", orderId);
        Contract.Require("Order exists", () => _orders.ContainsKey(orderId));

        _orders.Remove(orderId);

        Contract.Ensure("Order deleted", () => !_orders.ContainsKey(orderId));
    }
}
```

---

## Domain Events

Domain Events represent state changes. Use contracts to ensure events are raised correctly.

```csharp
using uContract;

public class Customer
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private string _email;
    private CustomerStatus _status;

    public void ChangeEmail(string newEmail)
    {
        Contract.RequireNotEmpty("Email", newEmail);

        if (Contract.Ignore("Email unchanged", () => _email == newEmail))
            return;

        var oldEmail = _email;
        _email = newEmail;

        // Raise domain event
        RaiseDomainEvent(new CustomerEmailChangedEvent(Id, oldEmail, newEmail));

        // Postcondition: Event raised
        Contract.Ensure("Email changed event raised",
            () => _domainEvents.OfType<CustomerEmailChangedEvent>().Any());
    }

    public void Deactivate()
    {
        Contract.Require("Customer must be active",
            () => _status == CustomerStatus.Active);

        _status = CustomerStatus.Deactivated;

        RaiseDomainEvent(new CustomerDeactivatedEvent(Id));

        Contract.Ensure("Status changed", () => _status == CustomerStatus.Deactivated);
        Contract.Ensure("Event raised",
            () => _domainEvents.OfType<CustomerDeactivatedEvent>().Any());
    }

    private void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        Contract.RequireNotNull("Domain event", domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        return _domainEvents.AsReadOnly();
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

public record CustomerEmailChangedEvent(string CustomerId, string OldEmail, string NewEmail) : IDomainEvent;
public record CustomerDeactivatedEvent(string CustomerId) : IDomainEvent;

public enum CustomerStatus { Active, Deactivated }
```

---

## Specifications

Specifications encapsulate business rules. Use with contracts for validation.

```csharp
using uContract;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T candidate);
}

public class CustomerIsEligibleForPremiumSpecification : ISpecification<Customer>
{
    private readonly int _minimumOrders;
    private readonly decimal _minimumTotalSpent;

    public CustomerIsEligibleForPremiumSpecification(int minimumOrders, decimal minimumTotalSpent)
    {
        Contract.Require("Minimum orders positive", () => minimumOrders > 0);
        Contract.Require("Minimum total positive", () => minimumTotalSpent > 0);

        _minimumOrders = minimumOrders;
        _minimumTotalSpent = minimumTotalSpent;
    }

    public bool IsSatisfiedBy(Customer candidate)
    {
        Contract.RequireNotNull("Candidate", candidate);

        return candidate.TotalOrders >= _minimumOrders
               && candidate.TotalSpent >= _minimumTotalSpent;
    }
}

// Usage in aggregate
public class Customer
{
    public void UpgradeToPremium()
    {
        var eligibilitySpec = new CustomerIsEligibleForPremiumSpecification(10, 1000m);

        // Precondition: Must satisfy specification
        Contract.Require("Customer eligible for premium",
            () => eligibilitySpec.IsSatisfiedBy(this));

        _membershipLevel = MembershipLevel.Premium;

        Contract.Ensure("Upgraded to premium",
            () => _membershipLevel == MembershipLevel.Premium);
    }
}
```

---

## Best Practices

### 1. Invariant Checking

✅ **DO**: Check invariants at aggregate boundaries
```csharp
public void AddItem(OrderItem item)
{
    _items.Add(item);
    CheckInvariant();  // ✅ After modification
}
```

❌ **DON'T**: Check invariants in the middle of methods
```csharp
public void AddItem(OrderItem item)
{
    _items.Add(item);
    CheckInvariant();  // ❌ Too early
    _total += item.Total;
    CheckInvariant();  // ✅ At boundary
}
```

### 2. State Capture

✅ **DO**: Capture state BEFORE modification
```csharp
public void Withdraw(decimal amount)
{
    var oldBalance = Contract.Old(() => _balance);  // ✅ Before
    _balance -= amount;
    Contract.Ensure(..., () => _balance == oldBalance - amount);
}
```

❌ **DON'T**: Capture state AFTER modification
```csharp
public void Withdraw(decimal amount)
{
    _balance -= amount;
    var oldBalance = Contract.Old(() => _balance);  // ❌ Too late!
}
```

### 3. Early Return Pattern

✅ **DO**: Use `Ignore()` for DDD early returns
```csharp
public void ChangeEmail(string newEmail)
{
    if (Contract.Ignore("Email unchanged", () => _email == newEmail))
        return;  // ✅ Skip unnecessary work

    // ... actual change logic
}
```

❌ **DON'T**: Use `Require()` for early returns
```csharp
public void ChangeEmail(string newEmail)
{
    Contract.Require("Email changed", () => _email != newEmail);  // ❌ Wrong - not a precondition
}
```

### 4. Immutability

✅ **DO**: Enforce immutability with contracts
```csharp
public Money Add(Money other)
{
    var result = new Money(Amount + other.Amount, Currency);
    return Contract.EnsureResult("New instance", result, r => r != this);  // ✅
}
```

### 5. Field Assignment Validation

✅ **DO**: Use `EnsureAssignable` for complex state changes
```csharp
public void ChangeEmail(string newEmail)
{
    var oldState = Contract.Old(() => this);
    _email = newEmail;
    _lastModified = DateTime.UtcNow;

    // Only email and lastModified should change
    Contract.EnsureAssignable(this, oldState,
        nameof(_email), nameof(_lastModified));  // ✅
}
```

---

## Common Patterns

### Pattern 1: Command Handler with Contracts

```csharp
public class TransferMoneyCommandHandler
{
    public void Handle(TransferMoneyCommand command)
    {
        // Preconditions: Validate command
        Contract.RequireNotNull("Command", command);
        Contract.Require("Amount positive", () => command.Amount > 0);
        Contract.Require("Different accounts",
            () => command.FromAccountId != command.ToAccountId);

        // Load aggregates
        var fromAccount = _repository.GetById(command.FromAccountId);
        var toAccount = _repository.GetById(command.ToAccountId);

        Contract.Check("From account exists", () => fromAccount != null);
        Contract.Check("To account exists", () => toAccount != null);

        // Execute business logic
        fromAccount!.Withdraw(command.Amount);
        toAccount!.Deposit(command.Amount);

        // Postcondition: Money conserved
        var totalBefore = Contract.Old(() =>
            fromAccount.GetBalance() + toAccount.GetBalance());
        Contract.Ensure("Money conserved",
            () => fromAccount.GetBalance() + toAccount.GetBalance() == totalBefore);
    }
}
```

### Pattern 2: Aggregate Factory

```csharp
public class OrderFactory
{
    public Order CreateOrder(string customerId, IEnumerable<OrderItem> items)
    {
        Contract.RequireNotEmpty("Customer ID", customerId);
        Contract.RequireNotNull("Items", items);
        Contract.Require("At least one item", () => items.Any());

        var orderId = Guid.NewGuid().ToString();
        var customer = new Customer(customerId, "");  // Simplified

        var order = new Order(orderId, customer);

        foreach (var item in items)
        {
            order.AddItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
        }

        Contract.EnsureNotNull("Order created", order);
        Contract.Ensure("Order has items", () => order.ItemCount > 0);

        return order;
    }
}
```

### Pattern 3: Saga with Contracts

```csharp
public class OrderFulfillmentSaga
{
    public void Execute(Order order)
    {
        Contract.RequireNotNull("Order", order);
        Contract.Require("Order submitted", () => order.Status == OrderStatus.Submitted);

        // Step 1: Reserve inventory
        ReserveInventory(order);
        Contract.Check("Inventory reserved", () => IsInventoryReserved(order));

        // Step 2: Process payment
        ProcessPayment(order);
        Contract.Check("Payment processed", () => order.Status == OrderStatus.Paid);

        // Step 3: Ship order
        ShipOrder(order);

        // Postcondition: Saga completed
        Contract.Ensure("Order shipped", () => order.Status == OrderStatus.Shipped);
        Contract.Ensure("Tracking number assigned", () => !string.IsNullOrEmpty(order.TrackingNumber));
    }

    private void ReserveInventory(Order order) { /* ... */ }
    private void ProcessPayment(Order order) { /* ... */ }
    private void ShipOrder(Order order) { /* ... */ }
    private bool IsInventoryReserved(Order order) { return true; /* ... */ }
}
```

---

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Contracts in Getters

```csharp
// ❌ DON'T: Contracts in simple getters
public decimal GetBalance()
{
    Contract.Check("Balance exists", () => _balance >= 0);  // ❌ Unnecessary
    return _balance;
}

// ✅ DO: No contracts in simple getters
public decimal GetBalance()
{
    return _balance;  // ✅ Simple getter
}
```

### ❌ Anti-Pattern 2: Validating Infrastructure

```csharp
// ❌ DON'T: Validate infrastructure concerns
public void SaveToDatabase()
{
    Contract.Check("Database connected", () => _dbContext != null);  // ❌ Infrastructure concern
    _dbContext.SaveChanges();
}

// ✅ DO: Validate domain rules only
public void Submit()
{
    Contract.Require("Order has items", () => _items.Count > 0);  // ✅ Domain rule
    _status = OrderStatus.Submitted;
}
```

### ❌ Anti-Pattern 3: Mixing Concerns

```csharp
// ❌ DON'T: Mix domain and application logic
public void PlaceOrder(Order order, IEmailService emailService)
{
    Contract.RequireNotNull("Order", order);
    Contract.RequireNotNull("Email service", emailService);  // ❌ Application service

    order.Submit();
    emailService.SendConfirmation(order);  // ❌ Application concern
}

// ✅ DO: Separate domain and application
public void Submit()  // Domain method
{
    Contract.Require("Order has items", () => _items.Count > 0);
    _status = OrderStatus.Submitted;
}

// Application service handles email
public class OrderApplicationService
{
    public void PlaceOrder(Order order)
    {
        order.Submit();
        _emailService.SendConfirmation(order);
    }
}
```

---

## Performance Considerations

### Production Configuration

**Disable contracts in production for zero overhead:**

```bash
# Production environment
export DBC=off
```

### Selective Enablement

**Enable only critical contracts in production:**

```bash
# Enable only invariants (most critical)
export DBC_PRE=off
export DBC_POST=off
export DBC_INV=on
export DBC_CHECK=off
```

### Optimize `Old<T>()` Usage

```csharp
// ❌ DON'T: Capture entire aggregate
public void AddItem(OrderItem item)
{
    var oldOrder = Contract.Old(() => this);  // ❌ Expensive deep copy
    // ...
}

// ✅ DO: Capture only what you need
public void AddItem(OrderItem item)
{
    var oldTotal = Contract.Old(() => _total);  // ✅ Capture primitive
    var oldItemCount = Contract.Old(() => _items.Count);  // ✅ Capture count
    // ...
}
```

---

## Testing DDD with Contracts

### Unit Testing Aggregates

```csharp
using Xunit;
using uContract.Exceptions;

public class BankAccountTests
{
    [Fact]
    public void Withdraw_ShouldThrowPreconditionViolation_WhenInsufficientFunds()
    {
        // Arrange
        var account = new BankAccount("123", "John Doe", 100);

        // Act & Assert
        var ex = Assert.Throws<PreconditionViolationException>(() =>
            account.Withdraw(150));

        Assert.Contains("Sufficient funds", ex.Message);
    }

    [Fact]
    public void Close_ShouldThrowInvariantViolation_WhenBalanceNotZero()
    {
        // Arrange
        var account = new BankAccount("123", "John Doe", 100);

        // Act & Assert - Invariant violated
        var ex = Assert.Throws<PreconditionViolationException>(() =>
            account.Close());

        Assert.Contains("Balance must be zero", ex.Message);
    }

    [Fact]
    public void Deposit_ShouldIncreaseBalance_WhenAmountValid()
    {
        // Arrange
        var account = new BankAccount("123", "John Doe", 100);

        // Act
        account.Deposit(50);

        // Assert - Postcondition satisfied
        Assert.Equal(150, account.GetBalance());
    }
}
```

### Integration Testing with Contracts Disabled

```csharp
public class OrderIntegrationTests : IDisposable
{
    public OrderIntegrationTests()
    {
        // Disable contracts for performance
        Environment.SetEnvironmentVariable("DBC", "off");
    }

    [Fact]
    public void ProcessOrder_ShouldComplete_WhenValid()
    {
        // Integration test without contract overhead
        var order = new Order("123", customer);
        order.AddItem("prod1", "Product 1", 10.00m, 2);
        order.Submit();

        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DBC", "on");
    }
}
```

---

## Real-World Example: E-Commerce Domain

Complete e-commerce domain with contracts.

```csharp
// See docs/examples/USAGE_EXAMPLES.md for complete implementation
// Key aggregates:
// - Order (aggregate root)
// - Customer (aggregate root)
// - Product (aggregate root)
// - Money (value object)
// - Address (value object)
// - OrderProcessor (domain service)
// - PaymentService (domain service)
```

---

## Summary

### Key Takeaways

1. ✅ **Aggregate Roots**: Check invariants after every public method
2. ✅ **Value Objects**: Validate in constructor, ensure immutability
3. ✅ **Entities**: Enforce lifecycle rules with invariants
4. ✅ **Domain Services**: Validate cross-aggregate business rules
5. ✅ **Event Sourcing**: Validate after event application
6. ✅ **Configuration**: Disable in production for zero overhead

### Contract Types by DDD Component

| DDD Component | Preconditions | Postconditions | Invariants |
|---------------|---------------|----------------|------------|
| Aggregate Root | ✅ All commands | ✅ State transitions | ✅ After all modifications |
| Value Object | ✅ Constructor | ✅ Operations | ✅ After construction |
| Entity | ✅ Commands | ✅ State changes | ✅ Lifecycle rules |
| Domain Service | ✅ Business rules | ✅ Cross-aggregate consistency | ❌ Stateless |
| Repository | ✅ Query parameters | ✅ Data integrity | ❌ Infrastructure |

---

## See Also

- [API Reference](examples/API_REFERENCE.md) - Complete API documentation
- [Usage Examples](examples/USAGE_EXAMPLES.md) - Practical examples
- [Architecture Decision Records](adr/) - Design decisions
- [README.md](../README.md) - Project overview

---

