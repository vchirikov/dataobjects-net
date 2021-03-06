// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Kofman
// Created:    2009.07.20

using NUnit.Framework;
using Xtensive.Orm.Configuration;
using Xtensive.Orm.Providers;

namespace Xtensive.Orm.Tests.Storage
{
  [TestFixture]
  public class CurrentSessionResolverTest
  {
    [TearDown]
    public void TearDown()
    {
      Session.Resolver = null;
    }

    [Test]
    public void Tests()
    {
      var config = DomainConfigurationFactory.Create();
      config.Sessions[WellKnown.Sessions.Default].Options = SessionOptions.ServerProfile;

      using (var domain = Domain.Build(config))
      using (var session = domain.OpenSession()) {

        bool isSessionActive = false;
        int resolveCount = 0;

        Session.Resolver = () => {
          resolveCount++;
          return isSessionActive ? session : null;
        };

        Assert.IsNull(Session.Current);

        Assert.AreEqual(1, resolveCount);
        Assert.IsFalse(session.IsActive);
        Assert.AreEqual(2, resolveCount);

        isSessionActive = true;

        Assert.AreEqual(session, Session.Current);
        Assert.AreEqual(3, resolveCount);
        Assert.IsTrue(session.IsActive);
        Assert.AreEqual(4, resolveCount);

        isSessionActive = false;

        using (session.Activate()) {
          Assert.AreEqual(session, Session.Current);
          Assert.IsTrue(session.IsActive);
        }

        Assert.AreEqual(4, resolveCount);
      }
    }
  }
}