// Copyright (C) a Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: a
// Created:    a

using NUnit.Framework;
using Xtensive.Core.Testing;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Issues.Issue0298_Model;
using System.Linq;

namespace Xtensive.Storage.Tests.Issues.Issue0298_Model
{
  [HierarchyRoot]
  public class Master : Entity
  {
    [Field, Key]
    public int Id { get; private set; }

    [Field]
    [Association("Master", OnOwnerRemove = OnRemoveAction.Cascade, OnTargetRemove = OnRemoveAction.Deny)]
    public EntitySet<MasterTrack> Tracks { get; private set; }
  }

  public class AudioMaster : Master
  {
  }

  [HierarchyRoot]
  public class MasterTrack : Entity
  {
    [Field, Key]
    public int Id { get; private set; }

    [Field]
    public Master Master { get; private set; }
  }
}

namespace Xtensive.Storage.Tests.Issues
{
  public class Issue0298_InheritedAssociationIsIgnored : AutoBuildTest
  {
    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(typeof (Master).Assembly, typeof (Master).Namespace);
      return config;
    }

    [Test]
    public void MainTest()
    {
      using (Session.Open(Domain)) {
        using (var t = Transaction.Open()) {

          var am = new AudioMaster();
          am.Tracks.Add(new MasterTrack());
          am.Tracks.Add(new MasterTrack());

          Assert.AreEqual(1, Query<AudioMaster>.All.Count());
          Assert.AreEqual(2, Query<MasterTrack>.All.Count());

          AssertEx.Throws<ReferentialIntegrityException>(() => am.Tracks.First().Remove());

          am.Remove();

          Assert.AreEqual(0, Query<AudioMaster>.All.Count());
          Assert.AreEqual(0, Query<MasterTrack>.All.Count());
          // Rollback
        }
      }
    }
  }
}