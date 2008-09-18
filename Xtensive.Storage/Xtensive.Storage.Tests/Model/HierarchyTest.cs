// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2007.12.26

using System;
using NUnit.Framework;
using Xtensive.Storage.Attributes;
using Xtensive.Storage.Building;
using Xtensive.Storage.Building.Definitions;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Model.Hierarchies;

namespace Xtensive.Storage.Tests.Model.Hierarchies
{
  public interface I0 : IEntity
  {
    [Field]
    string AName { get; set; }
  }

  public interface IA : I0
  {
  }

  public class A : Entity, IA
  {
    string I0.AName { get; set; }

    [Field]
    public string AName { get; set; }
  }

  [HierarchyRoot(typeof (Generator), "ID")]
  public class AB : A
  {
    [Field]
    public long ID { get; private set; }

    [Field]
    public string ABName { get; set; }
  }

  public class ABC : AB
  {
  }

  public abstract class B : Entity
  {
  }

  [HierarchyRoot(typeof (Generator), "ID")]
  public class BC : B
  {
    [Field]
    public Guid ID { get; private set; }
  }

  [HierarchyRoot(typeof (Generator), "ID")]
  public class BD : B
  {
    [Field]
    public long ID { get; private set; }

    [Field]
    public string AName { get; set; }
  }

  [HierarchyRoot(typeof (Generator), "ID")]
  public class BE : B
  {
    [Field]
    public int ID { get; private set; }
  }

  public class CustomStorageDefinitionBuilder : IDomainBuilder
  {
    public void Build(BuildingContext context, DomainModelDef model)
    {
      TypeDef type;

      type = model.Types[typeof(A)];
      Assert.IsFalse(context.Definition.FindRoot(type)==type);

      type = model.Types[typeof(AB)];
      Assert.IsTrue(context.Definition.FindRoot(type)==type);

      type = model.Types[typeof(ABC)];
      Assert.IsFalse(context.Definition.FindRoot(type)==type);

      type = model.Types[typeof(B)];
      Assert.IsFalse(context.Definition.FindRoot(type)==type);

      type = model.Types[typeof(BC)];
      Assert.IsTrue(context.Definition.FindRoot(type)==type);
    }
  }
}

namespace Xtensive.Storage.Tests.Model
{
  public class HierarchyTest : AutoBuildTest
  {
    protected override DomainConfiguration BuildConfiguration()
    {
      DomainConfiguration config = base.BuildConfiguration();
      config.Types.Register(typeof (A).Assembly, typeof(A).Namespace);
      config.Builders.Add(typeof (CustomStorageDefinitionBuilder));
      return config;
    }

    [Test]
    public void MainTest()
    {
      Assert.IsFalse(Domain.Model.Types.Contains(typeof (A)));
      Assert.IsNotNull(Domain.Model.Types[typeof (AB)]);
      Assert.IsNotNull(Domain.Model.Types[typeof (AB)].Fields["ID"]);
      Assert.IsNotNull(Domain.Model.Types[typeof (AB)].Fields["ABName"]);
      Assert.IsNotNull(Domain.Model.Types[typeof (AB)].Fields["AName"]);
      Assert.AreEqual(Domain.Model.Types[typeof (AB)], Domain.Model.Types[typeof (AB)].Fields["ABName"].DeclaringType);
      Assert.AreEqual(Domain.Model.Types[typeof (AB)], Domain.Model.Types[typeof (AB)].Fields["AName"].DeclaringType);
      Assert.AreEqual(Domain.Model.Types[typeof (AB)], Domain.Model.Types[typeof (AB)].Hierarchy.Root);
      Assert.AreEqual(Domain.Model.Types[typeof (AB)], Domain.Model.Types[typeof (ABC)].Hierarchy.Root);
      Assert.AreEqual(Domain.Model.Types[typeof (AB)].Hierarchy, Domain.Model.Types[typeof (ABC)].Hierarchy);
      Assert.AreEqual(typeof (long), Domain.Model.Types[typeof (AB)].Fields["ID"].ValueType);
      Assert.AreEqual(typeof (long), Domain.Model.Types[typeof (ABC)].Fields["ID"].ValueType);
      Assert.AreEqual(typeof (Guid), Domain.Model.Types[typeof (BC)].Fields["ID"].ValueType);
      Assert.AreEqual(typeof (long), Domain.Model.Types[typeof (BD)].Fields["ID"].ValueType);
      Assert.AreEqual(typeof (int), Domain.Model.Types[typeof (BE)].Fields["ID"].ValueType);
    }
  }
}