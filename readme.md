# Windows Deployment & Driver Management Tool

A lightweight, standalone C# utility designed to streamline Windows image servicing (DISM) and local system driver deployment. 

## ✨ Key Features
* **Driver Management:** One-click backup and restoration of working third-party system drivers.
* **WIM/ESD Servicing:** Strip unwanted Windows editions and slipstream drivers into specific image indices.
* **Solid Compression:** Convert heavy `.wim` files into compact, web-ready `.esd` archives.
* **Unattended Setup:** Generate automated deployment configurations to bypass modern Windows 11 hardware checks.

## 📋 Requirements
* **OS:** Windows 10 or Windows 11
* **Privileges:** Administrator Rights (The application will prompt for auto-elevation)

## 🚀 Getting Started
!. **Review the Guide:** Check out the step-by-step [Instructions](./instructions) file for an optimal configuration workflow.
2. **Inspect the Code:** The complete, frozen v1.0 source layout is available directly inside [ToolGui.cs](./ToolGui.cs).

---
*© 2026 Alice Kelly. Distributed under the standard MIT License.*
