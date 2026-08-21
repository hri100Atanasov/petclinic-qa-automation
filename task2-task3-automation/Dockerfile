# Playwright's .NET NuGet package embeds a driver that must match the browser
# binaries baked into this image exactly, or you get browser/driver mismatches
# at runtime. Keep this tag and the Microsoft.Playwright*/PackageReference
# versions in src/PetClinic.Tests.Ui/PetClinic.Tests.Ui.csproj in lockstep —
# bumping one without the other is the #1 way this image breaks.
ARG PLAYWRIGHT_VERSION=v1.62.0
FROM mcr.microsoft.com/playwright/dotnet:${PLAYWRIGHT_VERSION}-noble

WORKDIR /app

# Deliberately targets the two test projects directly rather than the .slnx —
# PetClinic.Tests.Runner (a host-only convenience tool that opens the HTML
# report in a browser after a local run) has nothing to do inside a headless
# container and is left out of the image entirely.
#
# Restore first, as its own layer, so source-only changes don't bust the
# (slow) package restore cache.
COPY src/PetClinic.Tests.Shared/PetClinic.Tests.Shared.csproj src/PetClinic.Tests.Shared/
COPY src/PetClinic.Tests.Ui/PetClinic.Tests.Ui.csproj src/PetClinic.Tests.Ui/
COPY src/PetClinic.Tests.Api/PetClinic.Tests.Api.csproj src/PetClinic.Tests.Api/
RUN dotnet restore src/PetClinic.Tests.Ui/PetClinic.Tests.Ui.csproj && \
    dotnet restore src/PetClinic.Tests.Api/PetClinic.Tests.Api.csproj

COPY src/PetClinic.Tests.Shared/ src/PetClinic.Tests.Shared/
COPY src/PetClinic.Tests.Ui/ src/PetClinic.Tests.Ui/
COPY src/PetClinic.Tests.Api/ src/PetClinic.Tests.Api/
RUN dotnet build --no-restore -c Release src/PetClinic.Tests.Ui/PetClinic.Tests.Ui.csproj && \
    dotnet build --no-restore -c Release src/PetClinic.Tests.Api/PetClinic.Tests.Api.csproj

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
CMD ["all"]
