// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Represents the state of an individual step in a process.
/// </summary>
[DataContract]
[KnownType(nameof(GetKnownTypes))]
public record KernelProcessStepState
{
    private readonly static HashSet<Type> s_knownTypes = [];

    private static HashSet<Type> GetKnownTypes() => s_knownTypes;

    /// <summary>
    /// The identifier of the Step which is required to be unique within an instance of a Process.
    /// This may be null until a process containing this step has been invoked.
    /// </summary>
    [DataMember(Name = "id")]
    public string? Id { get; init; }

    /// <summary>
    /// The name of the Step. This is intended to be human readable and is not required to be unique. If
    /// not provided, the name will be derived from the steps .NET type.
    /// </summary>
    [DataMember(Name = "name")]
    public string Name { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelProcessStepState"/> class.
    /// </summary>
    /// <param name="name">The name of the associated <see cref="KernelProcessStep"/></param>
    /// <param name="id">The Id of the associated <see cref="KernelProcessStep"/></param>
    public KernelProcessStepState(string name, string? id = null)
    {
        Verify.NotNullOrWhiteSpace(name);

        this.Id = id;
        this.Name = name;
    }

    public static void RegisterDerivedType(Type derivedType)
    {
        s_knownTypes.Add(derivedType);
    }
}

/// <summary>
/// Represents the state of an individual step in a process that includes a user-defined state object.
/// </summary>
/// <typeparam name="TState">The type of the user-defined state.</typeparam>
[DataContract]
public sealed record KernelProcessStepState<TState> : KernelProcessStepState where TState : class, new()
{
    /// <summary>
    /// The user-defined state object associated with the Step.
    /// </summary>
    [DataMember]
    public TState? State { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelProcessStepState"/> class.
    /// </summary>
    /// <param name="name">The name of the associated <see cref="KernelProcessStep"/></param>
    /// <param name="id">The Id of the associated <see cref="KernelProcessStep"/></param>
    public KernelProcessStepState(string name, string? id = null)
        : base(name, id)
    {
        Verify.NotNullOrWhiteSpace(name);

        this.Id = id;
        this.Name = name;
    }
}
