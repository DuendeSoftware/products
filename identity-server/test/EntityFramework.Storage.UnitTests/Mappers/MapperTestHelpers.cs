// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Reflection;

public static class MapperTestHelpers
{
    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Func<TSource, TDestination> mapper,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(DefaultConstructor<TSource>(), NoExclusions, EmptyCustomization<TSource>(), mapper, Array.Empty<string>(), out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Func<TSource> creator,
        Func<TSource, TDestination> mapper,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(creator, NoExclusions, EmptyCustomization<TSource>(), mapper, NoExclusions, out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Action<TSource> customizer,
        Func<TSource, TDestination> mapper,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(DefaultConstructor<TSource>(), NoExclusions, customizer, mapper, Array.Empty<string>(), out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Func<TSource, TDestination> mapper,
        IEnumerable<string> notMapped,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(DefaultConstructor<TSource>(), NoExclusions, EmptyCustomization<TSource>(), mapper, notMapped, out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Action<TSource> customizer,
        Func<TSource, TDestination> mapper,
        IEnumerable<string> notMapped,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(DefaultConstructor<TSource>(), NoExclusions, customizer, mapper, notMapped, out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        IEnumerable<string> excludeFromAutoInitialization,
        Action<TSource> customizer,
        Func<TSource, TDestination> mapper,
        IEnumerable<string> notMapped,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(DefaultConstructor<TSource>(), excludeFromAutoInitialization, customizer, mapper, notMapped, out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Func<TSource> creator,
        Action<TSource> customizer,
        Func<TSource, TDestination> mapper,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(creator, NoExclusions, customizer, mapper, NoExclusions, out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Func<TSource> creator,
        Func<TSource, TDestination> mapper,
        IEnumerable<string> notMapped,
        out List<string> unmappedMembers) => AllPropertiesAreMapped(creator, NoExclusions, EmptyCustomization<TSource>(), mapper, notMapped, out unmappedMembers);

    public static bool AllPropertiesAreMapped<TSource, TDestination>(
        Func<TSource> creator,
        IEnumerable<string> notAutoInitialized,
        Action<TSource> customizer,
        Func<TSource, TDestination> mapper,
        IEnumerable<string> notMapped,
        out List<string> unmappedMembers)
    {
        // Create the source object
        var source = creator();

        // Initialize the source object with non-default values in all of its properties
        var sourceType = typeof(TSource);
        var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        foreach (var property in sourceProperties.Where(p => !notAutoInitialized.Contains(p.Name)))
        {
            property.SetValue(source, GetNonDefaultValue(property.PropertyType));
        }

        // Customize properties as needed
        customizer(source);

        // Map from source to destination.
        var destination = mapper(source);

        // Now look for members that have default values in the destination value.
        // Everything that we included in our mapping will have mapped the 
        // non-default values. So we just check each property to see if it has the
        // default value.
        unmappedMembers = new List<string>();
        var destinationType = typeof(TDestination);
        var destinationProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in destinationProperties)
        {
            if (!notMapped.Contains(property.Name))
            {
                var propertyValue = property.GetValue(destination);

                if (propertyValue == null ||
                    propertyValue.Equals(GetDefaultValue(property.PropertyType)))
                {
                    unmappedMembers.Add(property.Name);
                }
            }
        }

        return unmappedMembers.Count == 0;
    }

    private static object GetDefaultValue(Type type)
    {
        if (type.IsAbstract ||
           type == typeof(string))
        {
            return null;
        }

        return Activator.CreateInstance(type);
    }

    private static object GetNonDefaultValue(Type type)
    {
        if (type == typeof(int))
        {
            return int.MaxValue;
        }

        if (type == typeof(long))
        {
            return long.MaxValue;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(string))
        {
            return "Non-empty string";
        }

        if (type == typeof(DateTime))
        {
            return DateTime.MaxValue;
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.MaxValue;
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(values.Length - 1);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
        {
            var itemType = type.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(itemType);
            var collection = Activator.CreateInstance(listType);

            // Add a non-default item to the collection
            var nonDefaultValue = GetNonDefaultValue(itemType);
            listType.GetMethod("Add")?.Invoke(collection, new[] { nonDefaultValue });

            return collection;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dictionary = Activator.CreateInstance(dictionaryType);

            // Add a non-default item to the dictionary
            var nonDefaultKey = GetNonDefaultValue(keyType);
            var nonDefaultValue = GetNonDefaultValue(valueType);
            dictionaryType.GetMethod("Add")?.Invoke(dictionary, new[] { nonDefaultKey, nonDefaultValue });

            return dictionary;
        }

        if (type.IsValueType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                return GetNonDefaultValue(underlyingType);
            }

            throw new Exception($"Value type {type.Name} not initialized by test framework. Add a case to GetNonDefaultValue for the type that will be different from the default.");
        }

        return Activator.CreateInstance(type);
    }


    private static Action<TSource> EmptyCustomization<TSource>() => src => { };

    private static Func<TSource> DefaultConstructor<TSource>() => Activator.CreateInstance<TSource>;

    private static string[] NoExclusions = Array.Empty<string>();
}
