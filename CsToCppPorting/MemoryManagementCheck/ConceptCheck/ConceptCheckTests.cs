using System;
using System.Collections.Generic;
using System.Linq;
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
        public InstanceNode(Int32 id, String typeName, InstanceNodeType nodeType)
        {
            Id = id;
            TypeName = typeName;
            NodeType = nodeType;
            Children = new List<PtrLink>();
        }

        public Int32 Id { get; }

        public String TypeName { get; }

        public InstanceNodeType NodeType { get; }

        public IList<PtrLink> Children { get; }
    }

    public class InstanceGraphSnapshot
    {
        public InstanceGraphSnapshot(InstanceNode root, IList<InstanceNode> nodes)
        {
            Root = root;
            Nodes = nodes;
        }

        public InstanceNode Root { get; }

        public IList<InstanceNode> Nodes { get; }
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

        public InstanceGraphSnapshot CreateSnapshot(Object rootObject)
        {
            if (rootObject == null)
                return null;
            IList<InstanceNode> nodes = new List<InstanceNode>();
            InstanceNode rootNode = Create(rootObject, new Dictionary<Object, InstanceNode>(), nodes);
            return new InstanceGraphSnapshot(rootNode, nodes);
        }

        private InstanceNode Create(Object current, IDictionary<Object, InstanceNode> instanceNodeMap, IList<InstanceNode> nodes)
        {
            ObjectContainer currentContainer = new ObjectContainer(current);
            if (instanceNodeMap.ContainsKey(currentContainer))
                return instanceNodeMap[currentContainer];
            String typeName = current.GetType().FullName;
            InstanceNodeType nodeType = typeName.StartsWith(_internalNamespacePrefix) ? InstanceNodeType.Internal : InstanceNodeType.External;
            InstanceNode node = new InstanceNode(nodes.Count + 1, typeName, nodeType);
            instanceNodeMap.Add(currentContainer, node);
            nodes.Add(node);
            if (nodeType == InstanceNodeType.Internal)
            {
                if (current.GetType().IsArray)
                    ProcessArray(current, instanceNodeMap, nodes, node);
                else
                    ProcessComplexObject(current, instanceNodeMap, nodes, node);
            }
            return node;
        }

        private void ProcessArray(Object current, IDictionary<Object, InstanceNode> instanceNodeMap, IList<InstanceNode> nodes, InstanceNode currentNode)
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
                InstanceNode child = Create(element, instanceNodeMap, nodes);
                currentNode.Children.Add(new PtrLink(CppPtrType.Strong, child));
            }
        }

        private void ProcessComplexObject(Object current, IDictionary<Object, InstanceNode> instanceNodeMap, IList<InstanceNode> nodes, InstanceNode currentNode)
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
                InstanceNode fieldNode = Create(value, instanceNodeMap, nodes);
                currentNode.Children.Add(new PtrLink(isWeakPtr ? CppPtrType.Weak : CppPtrType.Strong, fieldNode));
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
        [Test]
        public void PrintSingleObjects()
        {
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            InstanceGraphFactory graphFactory = new InstanceGraphFactory("SomeNamespaceB.OtherNamespaceB");
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.SomeOtherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(otherObj));
            Console.WriteLine();
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(anotherObj));
        }

        [Test]
        public void PrintSimpleCycleRefsObjects()
        {

            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj1 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj2 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            otherObj1.RefOther = otherObj2;
            otherObj2.RefOther = otherObj1;
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj1 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj2 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            anotherObj1.RefAnother = anotherObj2;
            anotherObj2.RefAnother = anotherObj1;
            InstanceGraphFactory graphFactory = new InstanceGraphFactory("SomeNamespaceB.OtherNamespaceB");
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.SomeOtherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(otherObj1));
            Console.WriteLine();
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(anotherObj1));
        }

        [Test]
        public void PrintSimpleCycleWeakRefsObjects()
        {
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj1 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj2 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            otherObj1.WeakRefOther = otherObj2;
            otherObj2.WeakRefOther = otherObj1;
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj1 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj2 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            anotherObj1.WeakRefAnother = anotherObj2;
            anotherObj2.WeakRefAnother = anotherObj1;
            InstanceGraphFactory graphFactory = new InstanceGraphFactory("SomeNamespaceB.OtherNamespaceB");
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.SomeOtherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(otherObj1));
            Console.WriteLine();
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(anotherObj1));
        }

        [Test]
        public void PrintComplexObjectsGraph()
        {
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj1 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj2 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj3 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj4 = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            otherObj1.RefsOther = new[] {otherObj2, otherObj3};
            otherObj2.WeakRefOther = otherObj1;
            otherObj2.RefOther = otherObj4;
            otherObj3.RefOther = otherObj1;
            otherObj3.WeakRefOther = otherObj3;
            otherObj3.RefsOther = new[] {otherObj4};
            otherObj4.RefsOther = new[] {otherObj1, otherObj2};
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj1 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj2 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj3 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj4 = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            anotherObj1.RefAnother = anotherObj2;
            anotherObj1.WeakRefAnother = anotherObj3;
            anotherObj1.RefOther = otherObj1;
            anotherObj2.RefsAnother = new[] {anotherObj3, anotherObj4};
            anotherObj2.WeakRefOther = otherObj2;
            anotherObj3.WeakRefOther = otherObj4;
            anotherObj3.WeakRefAnother = anotherObj1;
            anotherObj4.RefOther = otherObj2;
            anotherObj4.RefAnother = anotherObj1;
            InstanceGraphFactory graphFactory = new InstanceGraphFactory("SomeNamespaceB.OtherNamespaceB");
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.SomeOtherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(otherObj1));
            Console.WriteLine();
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(anotherObj1));
        }

        [Test]
        public void PrintExternalObjectsGraph()
        {
            SomeNamespaceA.SomeA someAObj1 = new SomeNamespaceA.SomeA();
            SomeNamespaceA.SomeA someAObj2 = new SomeNamespaceA.SomeA();
            SomeNamespaceA.SomeA someAObj3 = new SomeNamespaceA.SomeA();
            SomeNamespaceA.SomeB someBObj1 = new SomeNamespaceA.SomeB();
            SomeNamespaceA.SomeB someBObj2 = new SomeNamespaceA.SomeB();
            SomeNamespaceA.SomeB someBObj3 = new SomeNamespaceA.SomeB();
            someAObj1.RefsA = new[] {someAObj2, someAObj3};
            someAObj1.WeakRefB = someBObj1;
            someAObj2.WeakRefA = someAObj1;
            someAObj2.RefsB = new[] {someBObj1, someBObj3};
            someAObj3.RefA = someAObj1;
            someAObj3.WeakRefA = someAObj2;
            someAObj3.RefsB = new[] {someBObj2};
            someBObj1.RefA = someAObj3;
            someBObj1.WeakRefB = someBObj2;
            someBObj2.RefsB = new[] {someBObj3};
            someBObj3.RefsA = new[] {someAObj1, someAObj2, someAObj3};
            SomeNamespaceB.SomeClass someObj1 = new SomeNamespaceB.SomeClass();
            SomeNamespaceB.SomeClass someObj2 = new SomeNamespaceB.SomeClass();
            SomeNamespaceB.SomeClass someObj3 = new SomeNamespaceB.SomeClass();
            someObj1.RefsSome = new[] {someObj2};
            someObj1.WeakRefSome = someObj3;
            someObj1.RefA = someAObj2;
            someObj1.WeakRefB = someBObj3;
            someObj2.RefsSome = new[] {someObj1, someObj3};
            someObj2.WeakRefSome = someObj2;
            someObj3.RefA = someAObj1;
            someObj3.WeakRefA = someAObj3;
            someObj3.RefB = someBObj2;
            someObj3.WeakRefB = someBObj2;
            SomeNamespaceB.OtherNamespaceB.SomeOtherClass otherObj = new SomeNamespaceB.OtherNamespaceB.SomeOtherClass();
            otherObj.RefA = someAObj3;
            otherObj.WeakRefA = someAObj1;
            otherObj.RefB = someBObj2;
            otherObj.WeakRefB = someBObj3;
            otherObj.RefSome = someObj1;
            otherObj.WeakRefSome = someObj3;
            SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass anotherObj = new SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass();
            anotherObj.RefA = someAObj2;
            anotherObj.WeakRefA = someAObj1;
            anotherObj.RefB = someBObj3;
            anotherObj.WeakRefB = someBObj1;
            anotherObj.RefSome = someObj2;
            anotherObj.WeakRefSome = someObj1;
            InstanceGraphFactory graphFactory = new InstanceGraphFactory("SomeNamespaceB.OtherNamespaceB");
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.SomeOtherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(otherObj));
            Console.WriteLine();
            Console.WriteLine("for SomeNamespaceB.OtherNamespaceB.AnotherNamespaceB.SomeAnotherClass type:");
            PrintInstanceGraphSnapshot(graphFactory.CreateSnapshot(anotherObj));
        }

        private void PrintInstanceGraphSnapshot(InstanceGraphSnapshot snapshot)
        {
            foreach (InstanceNode node in snapshot.Nodes)
            {
                Console.WriteLine("Node with id = {0}, type = {1}, instance type = {2}", node.Id, node.NodeType, node.TypeName);
                IList<PtrLink> strongPtrs = node.Children.Where(child => child.PtrType == CppPtrType.Strong).ToList();
                if (strongPtrs.Count > 0)
                    Console.WriteLine("Strong ptr links: {0}", String.Join(", ", strongPtrs.Select(ptr => ptr.ToNode.Id)));
                IList<PtrLink> weakPtrs = node.Children.Where(child => child.PtrType == CppPtrType.Weak).ToList();
                if (weakPtrs.Count > 0)
                    Console.WriteLine("Weak ptr links: {0}", String.Join(", ", weakPtrs.Select(ptr => ptr.ToNode.Id)));
            }
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
