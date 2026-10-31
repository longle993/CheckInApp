FROM node:24-bookworm-slim AS android-tools
RUN apt-get update && apt-get install -y --no-install-recommends openjdk-17-jdk-headless curl unzip git ca-certificates python3 && rm -rf /var/lib/apt/lists/*
COPY .local/certs/ /usr/local/share/ca-certificates/checkin-host/
RUN update-ca-certificates
ENV NODE_OPTIONS=--use-system-ca
ENV JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64
ENV ANDROID_HOME=/opt/android-sdk
ENV ANDROID_SDK_ROOT=/opt/android-sdk
ENV PATH="/opt/android-sdk/cmdline-tools/latest/bin:/opt/android-sdk/platform-tools:${PATH}"
RUN mkdir -p /opt/android-sdk/cmdline-tools && \
    curl --fail --location --retry 8 --retry-all-errors --continue-at - --connect-timeout 30 --max-time 1200 https://dl.google.com/android/repository/commandlinetools-linux-15859902_latest.zip -o /tmp/tools.zip && \
    echo '4e4c464f145a7512b57d088ac6c278c03c9eea610886b35a5e0804e74eedf583  /tmp/tools.zip' | sha256sum -c - && \
    unzip -q /tmp/tools.zip -d /tmp/tools && mv /tmp/tools/cmdline-tools /opt/android-sdk/cmdline-tools/latest && rm -rf /tmp/tools /tmp/tools.zip
RUN yes | sdkmanager --licenses >/dev/null
FROM android-tools AS builder
RUN sdkmanager 'platform-tools' 'platforms;android-37.0' 'build-tools;37.0.0' 'ndk;27.1.12297006' 'cmake;3.22.1'
WORKDIR /work
