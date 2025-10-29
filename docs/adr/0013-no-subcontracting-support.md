# ADR-0013: No Subcontracting Support

## Status

**Accepted**

- **Date**: 2025-10-29
- **Deciders**: Project maintainers
- **Status Date**: 2025-10-29

---

## Context

### Problem Statement

Traditional Design by Contract (DbC) extensions (e.g., Eiffel, JML, Spec#) support **subcontracting**—the automatic derivation of contracts in inheritance hierarchies. When a derived class overrides a method with a contract, the subcontracting rules define how the derived contract relates to the base contract:

- **Precondition**: Derived precondition = Base precondition **OR** Derived precondition (weaker or equal)
- **Postcondition**: Derived postcondition = Base postcondition **AND** Derived postcondition (stronger or equal)
- **Class Invariant**: Derived invariant = Base invariant **AND** Derived invariant (stronger or equal)

We need to decide whether uContract.NET should support these subcontracting rules.

### Relevant Context

**From Root Contracting Paper (Section 4.3)**:
- Root contracting is a **constrained form of DbC**
- Only **aggregate roots** are typically contracted, not internal objects within aggregates
- uContract is a **utility library**, not a language extension (no custom compiler/preprocessor)
- The paper explicitly states: "we have an opportunity to keep uContract simple was **not to support subcontracting**"

**From DDD Best Practices**:
- Vernon (2013) and Millett & Tune (2015) advise against using polymorphic aggregates through inheritance
- Aggregate roots typically do not form inheritance hierarchies
- Polymorphism should be applied to **internal objects** within aggregates, not at the aggregate root level

**Implementation Complexity**:
- Eiffel supports subcontracting natively with reserved words
- DbC extensions (JML, Spec#) require custom compilers or postprocessors
- Implementing subcontracting without language support requires complex tooling:
  - Contract compilation (translate contracts into executable code)
  - Contract derivation (combine base and derived contracts with OR/AND)
  - Method weaving (inject combined contracts at appropriate points)

### Constraints

- Must maintain **zero-dependency principle** (ADR-0011)
- Must use only **standard C# language features** (no custom compiler/preprocessor)
- Must remain **language-independent** (easily portable to Go, Python, Rust, etc.)
- Must keep design **simple** (utility library, not a framework)

---

## Decision

**We will NOT support subcontracting (contract inheritance in derived classes). Contracts apply only to aggregate roots, and inheritance with contracts is not supported at the aggregate root level.**

### Details

**1. Scope of Contracting**

Contracts are specified **only at aggregate roots**, not at any internal class hierarchy:

```csharp
// ✅ SUPPORTED: Contracts at aggregate root
public class User : EsAggregateRoot
{
    public void ChangeEmail(string newEmail)
    {
        Contract.RequireNotNull("Email", newEmail);
        // ...
        Contract.Ensure("Email changed", () => _email == newEmail);
    }
}

// ✅ SUPPORTED: Inheritance within aggregate (no contracts on internal objects)
public abstract class PaymentMethod
{
    public abstract void ProcessPayment(decimal amount);
}

public class CreditCard : PaymentMethod
{
    public override void ProcessPayment(decimal amount)
    {
        // No contracts here (internal object)
    }
}
```

**2. No Automatic Contract Derivation**

uContract does **not** implement subcontracting rules. If a derived class overrides a contracted method, developers must **manually** write the combined contract:

```csharp
// ❌ NOT SUPPORTED: Automatic contract derivation
public abstract class BaseAggregate : EsAggregateRoot
{
    public virtual void Execute(string param)
    {
        Contract.Require("param not null", () => param != null);  // Base precondition
        // ...
    }
}

public class DerivedAggregate : BaseAggregate
{
    public override void Execute(string param)
    {
        // ❌ NO AUTOMATIC DERIVATION: uContract does not combine this with base precondition
        Contract.Require("param not null OR special case",
            () => param != null || IsSpecialCase());

        base.Execute(param);
    }
}
```

**3. Recommended Practice: Avoid Inheritance at Aggregate Root Level**

Following DDD best practices, aggregate roots should **not** use inheritance hierarchies:

```csharp
// ✅ RECOMMENDED: Single aggregate root, no inheritance
public class Order : EsAggregateRoot
{
    private List<OrderLine> _lines = new();  // Composition, not inheritance

    public void AddLine(Product product, int quantity)
    {
        Contract.RequireNotNull("Product", product);
        Contract.Require("Positive quantity", () => quantity > 0);

        _lines.Add(new OrderLine(product, quantity));

        Contract.Ensure("Line added", () => _lines.Count > 0);
    }

    protected override void EnsureInvariant()
    {
        Contract.Invariant("Order has lines", () => _lines.Count > 0);
        Contract.Invariant("Total is positive", () => CalculateTotal() >= 0);
    }
}
```

**4. Workaround for Required Inheritance**

If inheritance at the aggregate root level is unavoidable, developers must **manually compose** contracts:

```csharp
public abstract class BaseAggregate : EsAggregateRoot
{
    protected void ValidateBasePrecondition(string param)
    {
        Contract.Require("Base: param not null", () => param != null);
    }

    protected void ValidateBasePostcondition()
    {
        Contract.Ensure("Base: state valid", () => IsValid());
    }
}

public class DerivedAggregate : BaseAggregate
{
    public void Execute(string param)
    {
        // Manual composition: Base OR Derived precondition
        var baseSatisfied = param != null;
        var derivedSatisfied = IsSpecialCase();
        Contract.Require("Combined precondition", () => baseSatisfied || derivedSatisfied);

        // ... execute logic ...

        // Manual composition: Base AND Derived postcondition
        ValidateBasePostcondition();  // Base
        Contract.Ensure("Derived: extra condition", () => ExtraConditionMet());  // Derived
    }
}
```

---

## Consequences

### Positive Consequences

- ✅ **Simple utility design**: No complex contract derivation logic required
- ✅ **Language-independent**: Uses only standard C# features (lambda expressions, static methods)
- ✅ **Easily portable**: Can be ported to Go, Python, Rust, TypeScript without custom tooling
- ✅ **Zero-dependency maintained**: No need for custom compilers, preprocessors, or bytecode instrumentation
- ✅ **Clear scope**: Contract management focuses exclusively on aggregate roots
- ✅ **Faster development**: No need to implement and test subcontracting rules (OR/AND logic)
- ✅ **Aligns with DDD**: DDD best practices discourage polymorphic aggregate hierarchies
- ✅ **No toolchain dependency**: Developers don't need specialized build tools to use uContract

### Negative Consequences

- ❌ **No automatic contract derivation**: Developers must manually compose contracts in derived classes
- ❌ **Cannot use inheritance + contracts at aggregate root level**: Limits design options for some use cases
- ❌ **Developability drops to medium (from high)**: When comprehensive DbC with inheritance is needed, uContract is less suitable
- ❌ **Not suitable for polymorphic aggregate hierarchies**: Systems requiring extensive aggregate inheritance must use alternative approaches

### Neutral Consequences

- ⚖️ **Trade-off: Simplicity vs comprehensive DbC**: We prioritize simplicity and portability over full DbC feature set
- ⚖️ **DDD alignment reduces impact**: Most DDD-based systems don't need polymorphic aggregates at the root level
- ⚖️ **Polymorphism still available internally**: Developers can use inheritance within aggregates (for internal objects without contracts)
- ⚖️ **Different from Eiffel/JML**: Users familiar with traditional DbC tools will notice this limitation

---

## Alternatives Considered

### Alternative 1: Full Subcontracting Support

**Description**: Implement complete subcontracting rules (precondition OR, postcondition AND, invariant AND) like Eiffel.

**Pros**:
- Complete DbC functionality matching Meyer's theory
- Automatic contract derivation for derived classes
- No manual contract composition required
- Familiar to developers with Eiffel/JML/Spec# experience

**Cons**:
- **Requires custom compiler or postprocessor**: Violates zero-dependency principle (ADR-0011)
- **High implementation complexity**: Must parse contracts, apply derivation rules, and inject code
- **Difficult to maintain**: Custom tooling must stay synchronized with C# language updates
- **Not portable**: Each target language requires its own custom tooling
- **Breaks "utility library" design**: Becomes a framework requiring special build steps

**Why rejected**: Implementing subcontracting would require abandoning the zero-dependency and utility library principles that make uContract practical and portable. The complexity cost far outweighs the benefit, especially since DDD best practices discourage the use cases where subcontracting is needed.

---

### Alternative 2: Partial Subcontracting (Simple Cases Only)

**Description**: Support subcontracting only for simple inheritance (e.g., single inheritance, no interface contracts).

**Pros**:
- Partial DbC functionality with reduced complexity
- Could use reflection to detect inheritance at runtime
- Avoids most complex edge cases (multiple inheritance, mixins, interfaces)

**Cons**:
- **Unclear boundaries**: What qualifies as "simple"? Users will be confused about when it works
- **Incomplete solution**: Doesn't solve the general problem, just delays it
- **Still requires contract parsing**: Even simple cases need OR/AND logic implementation
- **Fragile implementation**: Relies on runtime reflection, prone to errors
- **Misleading**: Users expect full support once inheritance is involved

**Why rejected**: A half-implemented feature is worse than no feature. Users would encounter unpredictable behavior when their inheritance hierarchy exceeds the "simple" threshold. This creates a poor developer experience and maintenance burden.

---

### Alternative 3: Configurable Subcontracting Mode

**Description**: Allow users to enable/disable subcontracting via configuration:

```csharp
Contract.ConfigureSubcontracting(SubcontractingMode.Enabled);
```

**Pros**:
- Maximum flexibility for users
- Simple projects can use without subcontracting
- Complex projects can opt-in to subcontracting

**Cons**:
- **Configuration burden**: Requires setup code, violates zero-setup principle
- **Doubles testing complexity**: Must test both modes
- **Doubles maintenance cost**: Two separate code paths to maintain
- **User confusion**: Most users won't know which mode to choose
- **Still requires custom tooling**: Even with opt-in, implementation complexity remains

**Why rejected**: YAGNI (You Aren't Gonna Need It). The vast majority of DDD-based systems don't need subcontracting. Adding configuration for a rare use case introduces unnecessary complexity without meaningful benefit.

---

### Alternative 4: Documentation-Only Subcontracting

**Description**: Document how developers should manually implement subcontracting rules, but provide no library support.

**Pros**:
- Zero implementation cost
- Educates developers on DbC principles
- Provides guidance for edge cases

**Cons**:
- **No enforcement**: Developers can easily make mistakes in manual composition
- **Error-prone**: Forgetting to combine contracts with OR/AND leads to subtle bugs
- **Inconsistent**: Different developers will implement it differently
- **Not a real alternative**: This is essentially what we're doing by rejecting subcontracting

**Why rejected**: This is not an alternative; it's the consequence of our decision. We document the workaround in the "Implementation Notes" section.

---

## Related Decisions

- **Related to**: [ADR-0003 (API Design - Static Class Pattern)](0003-api-design-static-class.md) — Static design means no inheritance at Contract class level
- **Related to**: [ADR-0011 (Zero-Dependency Principle)](0011-zero-dependency-principle.md) — Custom tooling for subcontracting would violate this principle
- **Referenced by**: Root Contracting Paper, Section 4.3 ("Subcontracting"), Table 2 (Sustainability comparison showing uContract's simplicity advantage)

---

## Implementation Notes

### Design Guidelines for Developers

**1. Avoid Inheritance at Aggregate Root Level**

```csharp
// ❌ AVOID: Inheritance hierarchy at aggregate root level
public abstract class BaseOrder : EsAggregateRoot { }
public class CustomerOrder : BaseOrder { }
public class SupplierOrder : BaseOrder { }

// ✅ PREFER: Single aggregate root with composition
public class Order : EsAggregateRoot
{
    private OrderType _type;  // Enum: Customer, Supplier

    public void Execute()
    {
        switch (_type)
        {
            case OrderType.Customer:
                ExecuteCustomerLogic();
                break;
            case OrderType.Supplier:
                ExecuteSupplierLogic();
                break;
        }
    }
}
```

**2. Use Polymorphism Within Aggregates (Internal Objects)**

```csharp
// ✅ ALLOWED: Polymorphism for internal objects (no contracts)
public class Order : EsAggregateRoot
{
    private IPaymentStrategy _paymentStrategy;

    public void ProcessPayment(decimal amount)
    {
        Contract.Require("Positive amount", () => amount > 0);

        // Polymorphism at internal object level (no contracts on IPaymentStrategy)
        _paymentStrategy.Process(amount);

        Contract.Ensure("Payment processed", () => _paymentProcessed);
    }
}

public interface IPaymentStrategy
{
    void Process(decimal amount);  // No contracts
}

public class CreditCardPayment : IPaymentStrategy
{
    public void Process(decimal amount)
    {
        // No contracts here (internal object)
    }
}
```

**3. Manual Contract Composition (If Inheritance Required)**

If inheritance at the aggregate root level is unavoidable, manually compose contracts:

```csharp
public abstract class BaseAggregate : EsAggregateRoot
{
    protected virtual bool BasePrecondition(string param) => param != null;
    protected virtual bool BasePostcondition() => IsValid();
}

public class DerivedAggregate : BaseAggregate
{
    protected override bool BasePrecondition(string param) => base.BasePrecondition(param);
    protected virtual bool DerivedPrecondition(string param) => IsSpecialCase();

    public void Execute(string param)
    {
        // Manual OR: Base precondition OR Derived precondition
        Contract.Require("Combined precondition",
            () => BasePrecondition(param) || DerivedPrecondition(param));

        // ... execute logic ...

        // Manual AND: Base postcondition AND Derived postcondition
        Contract.Ensure("Base postcondition", () => BasePostcondition());
        Contract.Ensure("Derived postcondition", () => ExtraConditionMet());
    }
}
```

### Testing Strategy

**Focus on single aggregate roots**:
- Test contracts at the aggregate root level
- Do not test inheritance hierarchies with contracts
- Use composition instead of inheritance when possible

**For projects requiring inheritance**:
- Write explicit tests for manually composed contracts
- Verify OR logic for preconditions (base OR derived)
- Verify AND logic for postconditions (base AND derived)

---

## References

- [Root Contracting Paper - Section 4.3: Subcontracting](https://www.mdpi.com/2079-9292/14/21/4205)
- [Root Contracting Paper - Table 2: Key differences between root contracting and existing DbC extensions](https://www.mdpi.com/2079-9292/14/21/4205)
- [Meyer, B. (1997). Object-Oriented Software Construction, 2nd ed. - Chapter on Subcontracting](https://se.ethz.ch/~meyer/publications/computer/contract.pdf)
- [Vernon, V. (2013). Implementing Domain-Driven Design - Chapter 10: Aggregates](https://www.amazon.com/Implementing-Domain-Driven-Design-Vaughn-Vernon/dp/0321834577)
- [Millett, S., & Tune, N. (2015). Patterns, Principles, and Practices of Domain-Driven Design](https://www.amazon.com/Patterns-Principles-Practices-Domain-Driven-Design/dp/1118714709)
- [ADR-0003: API Design - Static Class Pattern](0003-api-design-static-class.md)
- [ADR-0011: Zero-Dependency Principle](0011-zero-dependency-principle.md)
- [DDD Reference - Aggregates Pattern](https://www.domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf)

---

## Revision History

| Date       | Status      | Notes                          |
|------------|-------------|--------------------------------|
| 2025-10-29 | Accepted    | Initial decision based on root contracting paper Section 4.3 |

---
