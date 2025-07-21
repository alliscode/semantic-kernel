# Simplified Local Runtime Architecture

This folder contains the simplified process execution architecture that replaces the complex inheritance-based LocalProcess system.

## Key Components

### Core Interfaces
- **IProcessStep**: Unified interface for all step types
- **IMessageBus**: Centralized message routing and event handling

### Core Classes
- **ProcessOrchestrator**: Main execution engine (replaces LocalProcess)
- **ProcessContext**: Maintains process hierarchy and shared resources
- **MessageBus**: Centralized message routing with edge processing
- **StepRegistry**: Manages step instances

### Step Implementations
- **FunctionStep**: Standard function-based steps
- **ProcessStepWrapper**: Sub-process execution wrapper
- **MapStep**: Map operations (TODO)
- **ProxyStep**: External proxy steps (TODO)
- **AgentStep**: Agent-based steps (TODO)

## Sub-Process Support

The architecture fully maintains sub-process functionality:

1. **Process-as-Step**: `ProcessStepWrapper` allows any `KernelProcess` to be executed as a step
2. **Hierarchical Context**: `ProcessContext.CreateChildContext()` maintains parent-child relationships
3. **Synchronous Execution**: Sub-processes execute within parent supersteps (maintains current behavior)
4. **Resource Sharing**: EventProxy, ExternalMessageChannel, and StorageManager are inherited by child processes
5. **Event Bubbling**: Events flow from child to parent through the MessageBus system

## Benefits

- **~50% Less Code**: Eliminates complex inheritance and duplicate logic
- **Clearer Separation**: Each component has a single responsibility
- **Better Testability**: Each component can be tested in isolation
- **Maintained Compatibility**: All existing behavior is preserved
- **Easier Extension**: Adding new step types is straightforward

## Migration Path

The `LocalKernelProcessContext` has been updated to use the new architecture while maintaining the same public API, ensuring backward compatibility.