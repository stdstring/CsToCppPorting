using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace CppAttributes
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class CppWeakPtr : System.Attribute
    {
    }
}

namespace SomeNamespaceA
{
    public class SomeA
    {
        public SomeA RefA;
        public SomeB RefB;
        public SomeA[] RefsA;
        public SomeB[] RefsB;
        [CppAttributes.CppWeakPtr]
        public SomeA WeakRefA;
        [CppAttributes.CppWeakPtr]
        public SomeB WeakRefB;
    }

    public class SomeB
    {
        public SomeA RefA;
        public SomeB RefB;
        public SomeA[] RefsA;
        public SomeB[] RefsB;
        [CppAttributes.CppWeakPtr]
        public SomeA WeakRefA;
        [CppAttributes.CppWeakPtr]
        public SomeB WeakRefB;
    }
}

namespace SomeNamespaceB
{
    public class SomeClass
    {
        public SomeNamespaceA.SomeA RefA;
        public SomeNamespaceA.SomeB RefB;
        [CppAttributes.CppWeakPtr]
        public SomeNamespaceA.SomeA WeakRefA;
        [CppAttributes.CppWeakPtr]
        public SomeNamespaceA.SomeB WeakRefB;

        public SomeClass RefSome;
        public SomeClass[] RefsSome;
        [CppAttributes.CppWeakPtr]
        public SomeClass WeakRefSome;
    }

    namespace OtherNamespaceB
    {
        public class SomeOtherClass
        {
            public SomeNamespaceA.SomeA RefA;
            public SomeNamespaceA.SomeB RefB;
            [CppAttributes.CppWeakPtr]
            public SomeNamespaceA.SomeA WeakRefA;
            [CppAttributes.CppWeakPtr]
            public SomeNamespaceA.SomeB WeakRefB;

            public SomeClass RefSome;
            [CppAttributes.CppWeakPtr]
            public SomeClass WeakRefSome;

            public SomeOtherClass RefOther;
            public SomeOtherClass[] RefsOther;
            [CppAttributes.CppWeakPtr]
            public SomeOtherClass WeakRefOther;
        }

        namespace AnotherNamespaceB
        {
            public class SomeAnotherClass
            {
                public SomeNamespaceA.SomeA RefA;
                public SomeNamespaceA.SomeB RefB;
                [CppAttributes.CppWeakPtr]
                public SomeNamespaceA.SomeA WeakRefA;
                [CppAttributes.CppWeakPtr]
                public SomeNamespaceA.SomeB WeakRefB;

                public SomeClass RefSome;
                [CppAttributes.CppWeakPtr]
                public SomeClass WeakRefSome;

                public SomeOtherClass RefOther;
                [CppAttributes.CppWeakPtr]
                public SomeOtherClass WeakRefOther;

                public SomeAnotherClass RefAnother;
                public SomeAnotherClass[] RefsAnother;
                [CppAttributes.CppWeakPtr]
                public SomeAnotherClass WeakRefAnother;
            }
        }
    }
}


namespace ConceptCheck
{
    public enum CppPtrType
    {
        Strong = 0,
        Weak = 1
    }

    public enum InstanceNodeType
    {
        // Internal nodes are interested for us
        Internal = 0,
        // External nodes aren't interested for us
        External = 1
    }

    public class InstanceNode
    {
        public InstanceNode(String typeName, InstanceNodeType nodeType)
        {
            TypeName = typeName;
            NodeType = nodeType;
            Children = new List<PtrLink>();
        }

        public String TypeName { get; }

        public InstanceNodeType NodeType { get; }

        public IList<PtrLink> Children { get; }
    }

    public class InstanceGraphSnapshot
    {
        public InstanceGraphSnapshot(InstanceNode root)
        {
            Root = root;
        }

        public InstanceNode Root { get; }
    }

    public class PtrLink
    {
        public PtrLink(CppPtrType ptrType, InstanceNode toNode)
        {
            PtrType = ptrType;
            ToNode = toNode;
        }

        public CppPtrType PtrType { get; }

        public InstanceNode ToNode { get; }
    }

    public class InstanceGraphFactory
    {
        public InstanceGraphFactory(String internalNamespacePrefix)
        {
            _internalNamespacePrefix = internalNamespacePrefix;
        }

        public InstanceGraphSnapshot CreateSnapshot(Object root)
        {
            if (root == null)
                return null;
            return new InstanceGraphSnapshot(Create(root, new Dictionary<Object, InstanceNode>()));
        }

        private InstanceNode Create(Object current, IDictionary<Object, InstanceNode> instanceNodeMap)
        {
            ObjectContainer currentContainer = new ObjectContainer(current);
            if (instanceNodeMap.ContainsKey(currentContainer))
                return instanceNodeMap[currentContainer];
            String typeName = current.GetType().FullName;
            InstanceNodeType nodeType = typeName.StartsWith(_internalNamespacePrefix) ? InstanceNodeType.Internal : InstanceNodeType.External;
            InstanceNode node = new InstanceNode(typeName, nodeType);
            instanceNodeMap.Add(currentContainer, node);
            if (nodeType == InstanceNodeType.Internal)
            {
                if (current.GetType().IsArray)
                    ProcessArray(current, instanceNodeMap, node);
                else
                    ProcessComplexObject(current, instanceNodeMap, node);
            }
            return node;
        }

        private void ProcessArray(Object current, IDictionary<Object, InstanceNode> instanceNodeMap, InstanceNode node)
        {
            Type elementType = current.GetType().GetElementType();
            if (elementType.IsValueType)
                return;
            Array array = (Array) current;
            // we support only 1D arrays now
            if (array.Rank > 1)
                throw new NotSupportedException();
            for (Int32 index = 0; index < array.GetLength(0); ++index)
            {
                Object element = array.GetValue(index);
                InstanceNode child = Create(element, instanceNodeMap);
                node.Children.Add(new PtrLink(CppPtrType.Strong, child));
            }
        }

        private void ProcessComplexObject(Object current, IDictionary<Object, InstanceNode> instanceNodeMap, InstanceNode node)
        {
            FieldInfo[] fields = current.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (FieldInfo field in fields)
            {
                Object value = field.GetValue(current);
                if (value == null)
                    continue;
                Type fieldType = field.FieldType;
                // value types isn't supported now
                if (fieldType.IsValueType)
                    continue;
                Boolean isWeakPtr = Attribute.GetCustomAttribute(field, typeof(CppAttributes.CppWeakPtr)) != null;
                InstanceNode fieldNode = Create(value, instanceNodeMap);
                node.Children.Add(new PtrLink(isWeakPtr ? CppPtrType.Weak : CppPtrType.Strong, fieldNode));
            }
        }

        private readonly String _internalNamespacePrefix;

        private class ObjectContainer
        {
            public ObjectContainer(Object obj)
            {
                InnerObj = obj;
            }

            public Object InnerObj { get; }

            public override Boolean Equals(Object obj)
            {
                if (obj == null)
                    return false;
                if (obj.GetType() != this.GetType())
                    return false;
                ObjectContainer other = (ObjectContainer) obj;
                return Object.ReferenceEquals(InnerObj, other.InnerObj);
            }

            public override int GetHashCode()
            {
                return InnerObj.GetHashCode();
            }
        }
    }

    [TestFixture]
    public class InstanceGraphTests
    {
        private void PrintInstanceGraph()
        {
        }
    }

    [TestFixture]
    public class AdditionalCheckTests
    {
        [Test]
        public void CheckObjectReferenceHolder()
        {
            SomeEntity obj1 = new SomeEntity(666, "IDDQD");
            SomeEntity obj2 = new SomeEntity(13, "IDKFA");
            SomeEntity obj3 = new SomeEntity(666, "IDDQD");
            ISet<Object> directObjSet = new HashSet<Object>(new[] {obj1, obj2, obj3});
            ISet<Object> containedObjSet = new HashSet<Object>(new[] {new ObjectContainer(obj1), new ObjectContainer(obj2), new ObjectContainer(obj3)});
            Assert.AreEqual(2, directObjSet.Count);
            Assert.AreEqual(3, containedObjSet.Count);
        }

        private class SomeEntity
        {
            public SomeEntity(Int32 id, String data)
            {
                Id = id;
                Data = data;
            }

            public Int32 Id { get; }

            public String Data { get; }

            public override Boolean Equals(Object obj)
            {
                if (obj == null)
                    return false;
                if (obj.GetType() != this.GetType())
                    return false;
                SomeEntity other = (SomeEntity) obj;
                return Id == other.Id;
            }

            public override Int32 GetHashCode()
            {
                return Id.GetHashCode();
            }
        }

        private class ObjectContainer
        {
            public ObjectContainer(Object obj)
            {
                InnerObj = obj;
            }

            public Object InnerObj { get; }

            public override Boolean Equals(Object obj)
            {
                if (obj == null)
                    return false;
                if (obj.GetType() != this.GetType())
                    return false;
                ObjectContainer other = (ObjectContainer) obj;
                return Object.ReferenceEquals(InnerObj, other.InnerObj);
            }

            public override int GetHashCode()
            {
                return InnerObj.GetHashCode();
            }
        }
    }
}
