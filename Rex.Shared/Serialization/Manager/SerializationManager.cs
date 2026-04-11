using System.Collections;
using System.Reflection;
using Rex.Shared.Serialization.Manager.Attributes;
using Rex.Shared.Serialization.Manager.Definition;

namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Reflection-based serialization manager for shared data definitions.
/// </summary>
public sealed class SerializationManager : ISerializationManager
{
    private readonly SerializationProvider _globalSerializers = new();
    private readonly Dictionary<Type, object> _customSerializerCache = [];

    /// <summary>
    /// Creates a serialization manager and registers type serializer attributes discovered in the current assembly.
    /// </summary>
    public SerializationManager()
    {
        RegisterAttributedSerializers(typeof(SerializationManager).Assembly);
    }

    /// <summary>
    /// Registers a serializer instance for a target type.
    /// </summary>
    /// <param name="targetType">Target type handled by the serializer.</param>
    /// <param name="serializer">Serializer instance.</param>
    public void RegisterSerializer(Type targetType, object serializer)
    {
        _globalSerializers.Register(targetType, serializer);
    }

    /// <summary>
    /// Registers serializer types marked with <see cref="TypeSerializerAttribute"/>.
    /// </summary>
    /// <param name="assembly">Assembly to scan.</param>
    public void RegisterAttributedSerializers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (Type type in assembly.GetTypes())
        {
            foreach (TypeSerializerAttribute attribute in type.GetCustomAttributes<TypeSerializerAttribute>())
            {
                object serializer = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException(
                        $"Failed to construct serializer type '{type.FullName}'.");
                _globalSerializers.Register(attribute.TargetType, serializer);
            }
        }
    }

    /// <inheritdoc />
    public T Read<T>(DataNode node, Func<T>? instanceProvider = null, bool notNullableOverride = false,
        ISerializationContext? context = null, bool skipHook = false)
    {
        object? value = Read(typeof(T), node, instanceProvider == null ? null : () => instanceProvider(), notNullableOverride,
            context, skipHook);
        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Failed to read value of type '{typeof(T).FullName}'.");
    }

    /// <summary>
    /// Reads a boxed value from a data node.
    /// </summary>
    /// <param name="type">Target type.</param>
    /// <param name="node">Source node.</param>
    /// <param name="instanceProvider">Optional instance provider for reuse.</param>
    /// <param name="notNullableOverride">Whether null values should be rejected for reference types.</param>
    /// <param name="context">Optional serialization context.</param>
    /// <param name="skipHook">Whether serialization hooks should be skipped.</param>
    /// <returns>Deserialized value.</returns>
    public object? Read(Type type, DataNode node, Func<object?>? instanceProvider = null, bool notNullableOverride = false,
        ISerializationContext? context = null, bool skipHook = false)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(node);

        if (TryGetSerializer(type, context, out object? serializer) && serializer is ITypeReader reader)
        {
            return reader.Read(this, type, node, notNullableOverride, context);
        }

        object? value = ReadInternal(type, node, instanceProvider, notNullableOverride, context, skipHook);
        if (!skipHook && value is ISerializationHook hook)
        {
            hook.AfterDeserialization();
        }

        return value;
    }

    /// <inheritdoc />
    public DataNode WriteValue<T>(T value, bool alwaysWrite = false, ISerializationContext? context = null, bool skipHook = false)
    {
        return WriteValue(typeof(T), value, alwaysWrite, context, skipHook);
    }

    /// <summary>
    /// Writes a boxed value to a data node.
    /// </summary>
    /// <param name="type">Runtime type to write.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="alwaysWrite">Whether default values must still be emitted.</param>
    /// <param name="context">Optional serialization context.</param>
    /// <param name="skipHook">Whether serialization hooks should be skipped.</param>
    /// <returns>Serialized data node.</returns>
    public DataNode WriteValue(Type type, object? value, bool alwaysWrite = false, ISerializationContext? context = null,
        bool skipHook = false)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!skipHook && value is ISerializationHook hook)
        {
            hook.BeforeSerialization();
        }

        if (TryGetSerializer(type, context, out object? serializer) && serializer is ITypeWriter writer)
        {
            return writer.Write(this, type, value, alwaysWrite, context);
        }

        return WriteInternal(type, value, alwaysWrite, context, skipHook);
    }

    /// <inheritdoc />
    public ValidationNode Validate<T>(DataNode node, ISerializationContext? context = null)
    {
        return Validate(typeof(T), node, context);
    }

    /// <summary>
    /// Validates a boxed node against a type.
    /// </summary>
    /// <param name="type">Target type.</param>
    /// <param name="node">Node to validate.</param>
    /// <param name="context">Optional serialization context.</param>
    /// <returns>Validation result tree.</returns>
    public ValidationNode Validate(Type type, DataNode node, ISerializationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(node);

        if (TryGetSerializer(type, context, out object? serializer) && serializer is ITypeValidator validator)
        {
            return validator.Validate(this, type, node, context);
        }

        try
        {
            _ = Read(type, node, null, notNullableOverride: false, context, skipHook: true);
            return new ValidationNode(valid: true);
        }
        catch (Exception exception)
        {
            return new ValidationNode(valid: false, exception.Message);
        }
    }

    /// <inheritdoc />
    public T CreateCopy<T>(T source, ISerializationContext? context = null, bool skipHook = false)
    {
        object? value = CreateCopy(typeof(T), source, context, skipHook);
        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Failed to create a copy of type '{typeof(T).FullName}'.");
    }

    /// <summary>
    /// Creates a deep copy of a boxed value.
    /// </summary>
    /// <param name="type">Runtime type to copy.</param>
    /// <param name="source">Source value.</param>
    /// <param name="context">Optional serialization context.</param>
    /// <param name="skipHook">Whether serialization hooks should be skipped.</param>
    /// <returns>Deep copy of the value.</returns>
    public object? CreateCopy(Type type, object? source, ISerializationContext? context = null, bool skipHook = false)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (source == null)
        {
            return null;
        }

        if (TryGetSerializer(type, context, out object? serializer) && serializer is ITypeCopier copier)
        {
            return copier.Copy(this, type, source, context, skipHook);
        }

        if (IsSimpleType(type))
        {
            return source;
        }

        if (source is IList list)
        {
            IList copyList = CreateListInstance(type, list.Count);
            Type elementType = GetListElementType(type);
            foreach (object? item in list)
            {
                object? copyItem = CreateCopy(elementType, item, context, skipHook);
                _ = copyList.Add(copyItem);
            }

            return ConvertListToRequestedType(type, copyList);
        }

        if (source is IDictionary dictionary)
        {
            IDictionary copyDictionary = CreateDictionaryInstance(type);
            Type valueType = GetDictionaryValueType(type);
            foreach (DictionaryEntry entry in dictionary)
            {
                copyDictionary.Add(entry.Key, CreateCopy(valueType, entry.Value, context, skipHook));
            }

            return copyDictionary;
        }

        if (IsDataDefinition(type))
        {
            object target = CreateInstance(type);
            CopyObjectMembers(source, target, context, skipHook);
            return target;
        }

        return source;
    }

    /// <inheritdoc />
    public void CopyTo<T>(T source, T target, ISerializationContext? context = null, bool skipHook = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        CopyTo(typeof(T), source!, target!, context, skipHook);
    }

    /// <summary>
    /// Copies values from one boxed instance to another compatible instance.
    /// </summary>
    /// <param name="type">Runtime type to copy.</param>
    /// <param name="source">Source instance.</param>
    /// <param name="target">Target instance.</param>
    /// <param name="context">Optional serialization context.</param>
    /// <param name="skipHook">Whether serialization hooks should be skipped.</param>
    public void CopyTo(Type type, object source, object target, ISerializationContext? context = null, bool skipHook = false)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        CopyObjectMembers(source, target, context, skipHook);
    }

    /// <inheritdoc />
    public MappingDataNode Compose<T>(params MappingDataNode[] nodes)
    {
        return (MappingDataNode)Compose(typeof(T), nodes);
    }

    /// <summary>
    /// Composes multiple nodes for one type.
    /// </summary>
    /// <param name="type">Target type.</param>
    /// <param name="nodes">Nodes to compose.</param>
    /// <returns>Composed mapping node.</returns>
    public DataNode Compose(Type type, params DataNode[] nodes)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(nodes);

        if (TryGetSerializer(type, null, out object? serializer) && serializer is ITypeComposer composer)
        {
            return composer.Compose(this, type, nodes, null);
        }

        MappingDataNode composed = new();
        foreach (DataNode node in nodes)
        {
            if (node is not MappingDataNode mapping)
            {
                throw new InvalidOperationException(
                    $"Compose for type '{type.FullName}' requires mapping nodes.");
            }

            foreach ((string key, DataNode value) in mapping.Values)
            {
                if (composed.TryGet(key, out DataNode existing) && existing is MappingDataNode existingMapping && value is MappingDataNode incomingMapping)
                {
                    composed.Set(key, Compose(typeof(object), existingMapping, incomingMapping));
                    continue;
                }

                composed.Set(key, value.Clone());
            }
        }

        return composed;
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(Guid)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan);
    }

    private static object CreateInstance(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type)!;
        }

        object? instance = Activator.CreateInstance(type);
        return instance ?? throw new InvalidOperationException(
            $"Type '{type.FullName}' must have a public parameterless constructor to be serialized.");
    }

    private object? ReadInternal(Type type, DataNode node, Func<object?>? instanceProvider, bool notNullableOverride,
        ISerializationContext? context, bool skipHook)
    {
        if (node is ValueDataNode valueNode)
        {
            return ReadScalar(type, valueNode, notNullableOverride);
        }

        if (TryReadList(type, node, context, skipHook, out object? list))
        {
            return list;
        }

        if (TryReadDictionary(type, node, context, skipHook, out object? dictionary))
        {
            return dictionary;
        }

        if (!IsDataDefinition(type))
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' is not supported by the reflection serializer.");
        }

        if (node is not MappingDataNode mapping)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' requires a mapping node for deserialization.");
        }

        object instance = instanceProvider?.Invoke() ?? CreateInstance(type);
        foreach (DataFieldDescriptor descriptor in GetDataFields(type))
        {
            DataNode? childNode = null;
            if (descriptor.IsInclude)
            {
                childNode = mapping;
            }
            else if (mapping.TryGet(descriptor.Tag, out DataNode resolvedNode))
            {
                childNode = resolvedNode;
            }

            if (childNode == null)
            {
                continue;
            }

            object? value = ReadMemberValue(descriptor, childNode, context, skipHook);
            descriptor.SetValue(instance, value);
        }

        return instance;
    }

    private DataNode WriteInternal(Type type, object? value, bool alwaysWrite, ISerializationContext? context, bool skipHook)
    {
        if (value == null)
        {
            return new ValueDataNode(null);
        }

        if (IsSimpleType(type))
        {
            return new ValueDataNode(FormatScalar(type, value));
        }

        if (value is IList list)
        {
            SequenceDataNode sequence = new();
            Type elementType = GetListElementType(type);
            foreach (object? item in list)
            {
                sequence.Sequence.Add(WriteValue(elementType, item, alwaysWrite, context, skipHook));
            }

            return sequence;
        }

        if (value is IDictionary dictionary)
        {
            MappingDataNode mapping = new();
            Type valueType = GetDictionaryValueType(type);
            foreach (DictionaryEntry entry in dictionary)
            {
                mapping.Set((string)entry.Key, WriteValue(valueType, entry.Value, alwaysWrite, context, skipHook));
            }

            return mapping;
        }

        if (!IsDataDefinition(type))
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' is not supported by the reflection serializer.");
        }

        object defaultInstance = CreateInstance(type);
        MappingDataNode output = new();
        List<(DataFieldDescriptor Descriptor, DataNode Node)> includeFields = [];
        foreach (DataFieldDescriptor descriptor in GetDataFields(type))
        {
            object? memberValue = descriptor.GetValue(value);
            object? defaultValue = descriptor.GetValue(defaultInstance);

            if (!alwaysWrite && descriptor.ShouldOmit(memberValue, defaultValue))
            {
                continue;
            }

            DataNode child = WriteMemberValue(descriptor, memberValue, alwaysWrite, context, skipHook);
            if (descriptor.IsInclude)
            {
                includeFields.Add((descriptor, child));
                continue;
            }

            output.Set(descriptor.Tag, child);
        }

        foreach ((DataFieldDescriptor descriptor, DataNode node) in includeFields)
        {
            if (node is not MappingDataNode includeMapping)
            {
                throw new InvalidOperationException(
                    $"Include data field '{descriptor.Member.Name}' on '{type.FullName}' must serialize to a mapping node.");
            }

            foreach ((string key, DataNode child) in includeMapping.Values)
            {
                if (!output.TryGet(key, out _))
                {
                    output.Set(key, child.Clone());
                }
            }
        }

        return output;
    }

    private object? ReadMemberValue(DataFieldDescriptor descriptor, DataNode childNode, ISerializationContext? context,
        bool skipHook)
    {
        if (descriptor.CustomSerializer is ITypeReader reader)
        {
            return reader.Read(this, descriptor.MemberType, childNode, notNullableOverride: false, context);
        }

        return Read(descriptor.MemberType, childNode, null, notNullableOverride: false, context, skipHook);
    }

    private DataNode WriteMemberValue(DataFieldDescriptor descriptor, object? memberValue, bool alwaysWrite,
        ISerializationContext? context, bool skipHook)
    {
        if (descriptor.CustomSerializer is ITypeWriter writer)
        {
            return writer.Write(this, descriptor.MemberType, memberValue, alwaysWrite, context);
        }

        return WriteValue(descriptor.MemberType, memberValue, alwaysWrite, context, skipHook);
    }

    private static object? ReadScalar(Type type, ValueDataNode node, bool notNullableOverride)
    {
        if (node.Value == null)
        {
            if (type.IsValueType || notNullableOverride)
            {
                throw new InvalidOperationException(
                    $"Null scalar cannot be read into non-nullable type '{type.FullName}'.");
            }

            return null;
        }

        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType == typeof(string))
        {
            return node.Value;
        }

        if (underlyingType == typeof(bool))
        {
            return bool.Parse(node.Value);
        }

        if (underlyingType == typeof(byte))
        {
            return byte.Parse(node.Value);
        }

        if (underlyingType == typeof(short))
        {
            return short.Parse(node.Value);
        }

        if (underlyingType == typeof(int))
        {
            return int.Parse(node.Value);
        }

        if (underlyingType == typeof(long))
        {
            return long.Parse(node.Value);
        }

        if (underlyingType == typeof(float))
        {
            return float.Parse(node.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(double))
        {
            return double.Parse(node.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(decimal))
        {
            return decimal.Parse(node.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(Guid))
        {
            return Guid.Parse(node.Value);
        }

        if (underlyingType.IsEnum)
        {
            return Enum.Parse(underlyingType, node.Value, ignoreCase: false);
        }

        throw new InvalidOperationException(
            $"Scalar type '{type.FullName}' is not supported by the reflection serializer.");
    }

    private static string? FormatScalar(Type type, object value)
    {
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType == typeof(string))
        {
            return (string)value;
        }

        if (underlyingType == typeof(float))
        {
            return ((float)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(double))
        {
            return ((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(decimal))
        {
            return ((decimal)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool TryReadList(Type type, DataNode node, ISerializationContext? context, bool skipHook, out object? list)
    {
        if (!TryGetListElementType(type, out Type? elementType))
        {
            list = null;
            return false;
        }

        if (node is not SequenceDataNode sequenceNode)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' requires a sequence node for deserialization.");
        }

        IList sequence = CreateListInstance(type, sequenceNode.Sequence.Count);
        foreach (DataNode child in sequenceNode.Sequence)
        {
            object? item = Read(elementType!, child, null, false, context, skipHook);
            _ = sequence.Add(item);
        }

        list = ConvertListToRequestedType(type, sequence);
        return true;
    }

    private bool TryReadDictionary(Type type, DataNode node, ISerializationContext? context, bool skipHook,
        out object? dictionary)
    {
        if (!TryGetDictionaryValueType(type, out Type? valueType))
        {
            dictionary = null;
            return false;
        }

        if (node is not MappingDataNode mappingNode)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' requires a mapping node for deserialization.");
        }

        IDictionary values = CreateDictionaryInstance(type);
        foreach ((string key, DataNode child) in mappingNode.Values)
        {
            object? value = Read(valueType!, child, null, false, context, skipHook);
            values.Add(key, value);
        }

        dictionary = values;
        return true;
    }

    private void CopyObjectMembers(object source, object target, ISerializationContext? context, bool skipHook)
    {
        Type type = source.GetType();
        foreach (DataFieldDescriptor descriptor in GetDataFields(type))
        {
            object? sourceValue = descriptor.GetValue(source);
            object? copiedValue = descriptor.CustomSerializer is ITypeCopier copier
                ? copier.Copy(this, descriptor.MemberType, sourceValue, context, skipHook)
                : CreateCopy(descriptor.MemberType, sourceValue, context, skipHook);
            descriptor.SetValue(target, copiedValue);
        }
    }

    private bool TryGetSerializer(Type type, ISerializationContext? context, out object? serializer)
    {
        if (context != null && context.SerializerProvider.TryGet(type, out serializer))
        {
            return true;
        }

        if (_globalSerializers.TryGet(type, out serializer))
        {
            return true;
        }

        serializer = null;
        return false;
    }

    private static bool IsDataDefinition(Type type)
    {
        if (type.IsDefined(typeof(DataDefinitionAttribute), inherit: true))
        {
            return true;
        }

        foreach (Attribute attribute in type.GetCustomAttributes(inherit: true).Cast<Attribute>())
        {
            if (attribute.GetType().IsDefined(typeof(MeansDataDefinitionAttribute), inherit: true))
            {
                return true;
            }
        }

        foreach (Type baseType in GetBaseTypesAndInterfaces(type))
        {
            if (baseType.IsDefined(typeof(ImplicitDataDefinitionForInheritorsAttribute), inherit: true))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<DataFieldDescriptor> GetDataFields(Type type)
    {
        List<DataFieldDescriptor> descriptors = [];
        foreach (MemberInfo member in GetDataMembers(type))
        {
            DataFieldBaseAttribute? attribute = member.GetCustomAttributes(true)
                .OfType<DataFieldBaseAttribute>()
                .FirstOrDefault();
            if (attribute == null)
            {
                continue;
            }

            descriptors.Add(new DataFieldDescriptor(
                member,
                GetMemberType(member),
                attribute.Tag ?? DataDefinitionUtility.AutoGenerateTag(member.Name),
                attribute is IncludeDataFieldAttribute,
                member.IsDefined(typeof(AlwaysPushInheritanceAttribute), inherit: true),
                member.IsDefined(typeof(NeverPushInheritanceAttribute), inherit: true),
                GetCustomSerializer(attribute.CustomTypeSerializer)));
        }

        return descriptors.OrderBy(static descriptor => descriptor.Tag, StringComparer.Ordinal);
    }

    private object? GetCustomSerializer(Type? serializerType)
    {
        if (serializerType == null)
        {
            return null;
        }

        if (_customSerializerCache.TryGetValue(serializerType, out object? serializer))
        {
            return serializer;
        }

        serializer = Activator.CreateInstance(serializerType)
            ?? throw new InvalidOperationException(
                $"Failed to construct custom serializer type '{serializerType.FullName}'.");
        _customSerializerCache.Add(serializerType, serializer);
        return serializer;
    }

    private static IEnumerable<MemberInfo> GetDataMembers(Type type)
    {
        Stack<Type> hierarchy = new();
        Type? current = type;
        while (current != null)
        {
            hierarchy.Push(current);
            current = current.BaseType;
        }

        while (hierarchy.Count > 0)
        {
            Type declaringType = hierarchy.Pop();
            MemberInfo[] members = declaringType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (MemberInfo member in members)
            {
                if (member is FieldInfo or PropertyInfo)
                {
                    yield return member;
                }
            }
        }
    }

    private static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => throw new InvalidOperationException($"Unsupported member type '{member.MemberType}'.")
        };
    }

    private static IEnumerable<Type> GetBaseTypesAndInterfaces(Type type)
    {
        Type? current = type.BaseType;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }

        foreach (Type @interface in type.GetInterfaces())
        {
            yield return @interface;
        }
    }

    private static bool TryGetListElementType(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        Type? listType = type
            .GetInterfaces()
            .Concat([type])
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IList<>));
        if (listType != null)
        {
            elementType = listType.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static Type GetListElementType(Type type)
    {
        return TryGetListElementType(type, out Type? elementType)
            ? elementType!
            : throw new InvalidOperationException($"Type '{type.FullName}' is not a supported list type.");
    }

    private static IList CreateListInstance(Type type, int capacity)
    {
        if (type.IsArray)
        {
            return new List<object?>(capacity);
        }

        Type concreteType = type.IsInterface || type.IsAbstract
            ? typeof(List<>).MakeGenericType(GetListElementType(type))
            : type;
        return (IList)(Activator.CreateInstance(concreteType)
            ?? throw new InvalidOperationException($"Failed to construct list type '{concreteType.FullName}'."));
    }

    private static bool TryGetDictionaryValueType(Type type, out Type? valueType)
    {
        Type? dictionaryType = type
            .GetInterfaces()
            .Concat([type])
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                && candidate.GetGenericArguments()[0] == typeof(string));
        if (dictionaryType != null)
        {
            valueType = dictionaryType.GetGenericArguments()[1];
            return true;
        }

        valueType = null;
        return false;
    }

    private static Type GetDictionaryValueType(Type type)
    {
        return TryGetDictionaryValueType(type, out Type? valueType)
            ? valueType!
            : throw new InvalidOperationException($"Type '{type.FullName}' is not a supported dictionary type.");
    }

    private static object ConvertListToRequestedType(Type type, IList list)
    {
        if (!type.IsArray)
        {
            return list;
        }

        Type elementType = GetListElementType(type);
        var array = Array.CreateInstance(elementType, list.Count);
        list.CopyTo(array, 0);
        return array;
    }

    private static IDictionary CreateDictionaryInstance(Type type)
    {
        Type concreteType = type.IsInterface || type.IsAbstract
            ? typeof(Dictionary<,>).MakeGenericType(typeof(string), GetDictionaryValueType(type))
            : type;
        return (IDictionary)(Activator.CreateInstance(concreteType)
            ?? throw new InvalidOperationException($"Failed to construct dictionary type '{concreteType.FullName}'."));
    }

    private sealed class DataFieldDescriptor
    {
        public DataFieldDescriptor(MemberInfo member, Type memberType, string tag, bool isInclude,
            bool alwaysPushInheritance, bool neverPushInheritance, object? customSerializer)
        {
            Member = member;
            MemberType = memberType;
            Tag = tag;
            IsInclude = isInclude;
            AlwaysPushInheritance = alwaysPushInheritance;
            NeverPushInheritance = neverPushInheritance;
            CustomSerializer = customSerializer;
        }

        public MemberInfo Member { get; }

        public Type MemberType { get; }

        public string Tag { get; }

        public bool IsInclude { get; }

        public bool AlwaysPushInheritance { get; }

        public bool NeverPushInheritance { get; }

        public object? CustomSerializer { get; }

        public object? GetValue(object instance)
        {
            return Member switch
            {
                FieldInfo field => field.GetValue(instance),
                PropertyInfo property => property.GetValue(instance),
                _ => throw new InvalidOperationException($"Unsupported member type '{Member.MemberType}'.")
            };
        }

        public void SetValue(object instance, object? value)
        {
            switch (Member)
            {
                case FieldInfo field:
                    field.SetValue(instance, value);
                    break;
                case PropertyInfo property:
                    property.SetValue(instance, value);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported member type '{Member.MemberType}'.");
            }
        }

        public bool ShouldOmit(object? value, object? defaultValue)
        {
            if (AlwaysPushInheritance)
            {
                return false;
            }

            return Equals(value, defaultValue);
        }
    }
}
