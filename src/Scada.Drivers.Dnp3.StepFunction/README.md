# Step Function DNP3 adapter

This project contains the optional Step Function-backed implementation of the EliteSCADA vendor-neutral DNP3 master-session contract.

The core DNP3 contracts and `Dnp3Driver` remain in `Scada.Drivers` and do not depend on Step Function. This adapter can therefore be omitted or replaced without changing the canonical EliteSCADA DNP3 runtime surface.

## Third-party licensing boundary

The current adapter uses the Step Function `dnp3` NuGet package version `1.6.0` for development and validation. The upstream `LICENSE.txt` for that release restricts the default license to non-commercial, non-production use and does not grant redistribution rights. Commercial/production use or redistribution requires a separate agreement with Step Function.

Do not make this project a mandatory dependency of the general EliteSCADA driver assembly or a production distribution until the applicable production/redistribution license has been approved.
