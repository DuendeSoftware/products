// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using Duende.IdentityServer.Configuration;
using Shouldly;
using Xunit;

namespace UnitTests.Services.Default.KeyManagement;

public class KeyManagerOptionsTests
{
    [Fact]
    public void InitializationSynchronizationDelay_should_be_greater_than_or_equal_to_zero()
    {
        var subject = new KeyManagementOptions
        {
            InitializationSynchronizationDelay = -TimeSpan.FromMinutes(1),
        };

        Action a = () => subject.Validate();
        a.ShouldThrow<Exception>();
    }

    [Fact]
    public void InitializationDuration_should_be_greater_than_or_equal_to_zero()
    {
        var subject = new KeyManagementOptions
        {
            InitializationDuration = -TimeSpan.FromMinutes(1),
        };

        Action a = () => subject.Validate();
        a.ShouldThrow<Exception>();
    }

    [Fact]
    public void InitializationKeyCacheDuration_should_be_greater_than_or_equal_to_zero()
    {
        var subject = new KeyManagementOptions
        {
            InitializationKeyCacheDuration = -TimeSpan.FromMinutes(1),
        };

        Action a = () => subject.Validate();
        a.ShouldThrow<Exception>();
    }

    [Fact]
    public void keycacheduration_should_be_greater_than_or_equal_to_zero()
    {
        var subject = new KeyManagementOptions
        {
            KeyCacheDuration = -TimeSpan.FromMinutes(1),
        };

        Action a = () => subject.Validate();
        a.ShouldThrow<Exception>();
    }

    [Fact]
    public void activation_should_be_greater_than_zero()
    {
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(0),
                RotationInterval = TimeSpan.FromMinutes(2),
                RetentionDuration = TimeSpan.FromMinutes(1)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = -TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(2),
                RetentionDuration = TimeSpan.FromMinutes(1)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }
    }

    [Fact]
    public void expiration_should_be_greater_than_zero()
    {
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(0),
                RetentionDuration = TimeSpan.FromMinutes(3)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = -TimeSpan.FromMinutes(1),
                RetentionDuration = TimeSpan.FromMinutes(2)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }
    }

    [Fact]
    public void retirement_should_be_greater_than_zero()
    {
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(2),
                RetentionDuration = TimeSpan.FromMinutes(0)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(2),
                RetentionDuration = -TimeSpan.FromMinutes(1)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }
    }

    [Fact]
    public void expiration_should_be_longer_than_activation_delay()
    {
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(1),
                RetentionDuration = TimeSpan.FromMinutes(10)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }

        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(2),
                RotationInterval = TimeSpan.FromMinutes(1),
                RetentionDuration = TimeSpan.FromMinutes(10)
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }

        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(2),
                RetentionDuration = TimeSpan.FromMinutes(10)
            };

            Action a = () => subject.Validate();
            a.ShouldNotThrow();
        }
    }

    [Fact]
    public void retirement_should_be_longer_than_expiration()
    {
        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(10),
                RetentionDuration = TimeSpan.FromMinutes(0),
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }

        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(10),
                RetentionDuration = -TimeSpan.FromMinutes(1),
            };

            Action a = () => subject.Validate();
            a.ShouldThrow<Exception>();
        }

        {
            var subject = new KeyManagementOptions
            {
                PropagationTime = TimeSpan.FromMinutes(1),
                RotationInterval = TimeSpan.FromMinutes(10),
                RetentionDuration = TimeSpan.FromMinutes(20),
            };

            Action a = () => subject.Validate();
            a.ShouldNotThrow();
        }
    }
}
