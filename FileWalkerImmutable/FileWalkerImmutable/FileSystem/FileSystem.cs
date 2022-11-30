using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWalkerImmutable
{
    // ChildrenMap maps the Guid of folders to the list of their children components.
    record FileSystem(ImmutableDictionary<Guid, ImmutableList<IComponent>> ChildrenMap,
        ImmutableDictionary<Guid, ImmutableList<IComponentObserver>> Observers)
    {
        public FileSystem() : this(
            ImmutableDictionary<Guid, ImmutableList<IComponent>>.Empty,
            ImmutableDictionary<Guid, ImmutableList<IComponentObserver>>.Empty)
        { }

        private void NotifyChange(IComponent before, IComponent after)
        {
            Observers.TryGetValue(before.ID, out var observers);
            if (observers != null)
            {
                foreach (var observer in observers)
                    observer.Notify(before, after);
            }
        }

        public IEnumerable<IComponent> Children(IComponent folder)
        {
            // Check if children exist, if not return empty list.
            if (ChildrenMap.TryGetValue(folder.ID, out var value))
                return value;
            else
                return Enumerable.Empty<IComponent>();
        }

        public FileSystem Add(IComponent parent, IComponent child)
        {
            // Add the child to the parent's children list if it has one, otherwise create a new children list.
            ChildrenMap.TryGetValue(parent.ID, out var value);
            var newChildren = value switch
            {
                null => ImmutableList.Create(child),
                var children => children.Add(child)
            };

            var newMap = ChildrenMap.SetItem(parent.ID, newChildren);
            return this with { ChildrenMap = newMap };
        }

        public FileSystem AddList(IComponent parent, IEnumerable<IComponent> children)
        {
            Func<FileSystem, IComponent, FileSystem> foldFn = (fs, child) => fs.Add(parent, child);
            return children.Aggregate(this, foldFn);
        }

        public FileSystem Remove(IComponent component)
        {
            NotifyChange(component, null);

            // Remove all children first.
            var fs = this;
            ChildrenMap.TryGetValue(component.ID, out var children);
            if (children != null) {
                foreach (var child in children)
                    fs = Remove(child);
            }

            // Make the map temporarily mutable so we can easily work with it.
            var builder = fs.ChildrenMap.ToBuilder();

            builder.Remove(component.ID);
            foreach(var kvp in fs.ChildrenMap)
            {
                if (kvp.Value.Contains(component))
                    builder[kvp.Key] = kvp.Value.Remove(component);
            }

            return fs with { ChildrenMap = builder.ToImmutable() };
        }

        public FileSystem Replace(IComponent oldComponent, IComponent newComponent)
        {
            NotifyChange(oldComponent, newComponent);

            // Make the map temporarily mutable so we can easily work with it.
            var builder = ChildrenMap.ToBuilder();

            builder.Remove(oldComponent.ID);

            ChildrenMap.TryGetValue(oldComponent.ID, out var oldChildren);
            builder.Add(newComponent.ID, oldChildren);

            foreach(var kvp in ChildrenMap)
            {
                if (kvp.Value?.Contains(oldComponent) == true)
                    builder[kvp.Key] = kvp.Value.Replace(oldComponent, newComponent);
            }

            return this with { ChildrenMap = builder.ToImmutable() };
        }

        public FileSystem Rename(IComponent component, string newName)
        {
            IComponent newComp = component switch
            {
                File file => file with { Name = newName },
                Folder folder => folder with { Name = newName },
                _ => throw new ArgumentException("Argument component is not a .")
            };

            return Replace(component, newComp);
        }

        public FileSystem Delete(IComponent component)
        {
            return Remove(component);
        }

        public FileSystem Attach(IComponent component, IComponentObserver observer)
        {
            // Add the observer to the parent's observer list if it has one, otherwise create a new observer list.
            Observers.TryGetValue(component.ID, out var value);
            var newObserverList = value switch
            {
                null => ImmutableList.Create(observer),
                var observers => observers.Add(observer)
            };

            var newObservers = Observers.SetItem(component.ID, newObserverList);
            return this with { Observers = newObservers };
        }

        public FileSystem Detach(IComponent component, IComponentObserver observer)
        {
            // Remove the observer from the parent's observer list if it exists, or throw an exception otherwise.
            Observers.TryGetValue(component.ID, out var value);
            var newObserverList = value switch
            {
                null => throw new ArgumentException("Observer does not exist in component."),
                var observers => observers.Remove(observer)
            };

            var newObservers = Observers.SetItem(component.ID, newObserverList);
            return this with { Observers = newObservers };
        }
    }
}
