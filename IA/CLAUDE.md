# ModbusApp - AI Context

## Project Overview
This project is a Modbus communication system written in C#. The software will be used to read and write Modbus registers on embedded devices
from the energy metering area. The initial focous will be the desktop cross platform version, with the mobile version coming later.
Both versions will share commom code using the Core of the project.

It consists of:
- Modbus.Core (shared logic)
- Modbus.Desktop (Avalonia UI)
- Modbus.Mobile (MAUI UI)

## Goals
- Support Modbus TCP and RTU
- Support and persist multiple devices simultaneously on database
- Local lightweight database
- Provide async communication
- Be reusable across UI platforms
- Modern UI on desktop with lateral navigation bar

## Architecture Guidelines
- Use clean architecture principles
- Separate transport, protocol, and application logic
- Avoid tight coupling between layers

## Core Concepts
- IModbusService is the main entry point
- Transport layer handles TCP/RTU
- Protocol layer handles Modbus framing
- Services orchestrate communication

## Constraints
- Must be thread-safe
- Must support background polling
- Must allow future scalability

## Coding Guidelines
- Use async/await
- Use interfaces for abstraction
- Avoid static/global state

## Future Plans
- Device manager
- Polling engine
- Reconnection strategy
- UI binding support