# XyGraph

**XyGraph** is a reusable **WPF node graph control** implemented as a .NET class library targeting **.NET 10**.  
It is designed to be embedded into other WPF applications as a configurable, extensible graph editing tool.

The library provides a canvas-backed graph model, a pan/zoom viewport, and primitives for building node-based editors such as dialogue systems, workflows, or visual scripting tools.

<img width="900" height="509" alt="image" src="https://github.com/user-attachments/assets/936f2f02-afe9-4a8f-b836-8fd5a39b811b" />


---

## Core Concepts

- **GraphView** — A `UserControl` that hosts the graph canvas and handles viewport interaction (pan, zoom, input).  
  > A GraphView is a viewport into a graph. Nodes are placed on the Graph, and panning moves the viewport over the graph.  

- **Graph** — A canvas-based container that stores nodes and edges, renders a grid, and manages graph-level serialization.

- **Node** — Visual element containing layout containers and ports. Extend to add custom behavior.

- **Port / Socket** — Input/output connection points that manage edge connections.

- **Edge** — Connection between two ports, with snapping and persistence handled at the graph level.

---


## Features

- Graphs can be saved and loaded to JSON files, allowing easy persistence and sharing of graph data.
- The base `Node` class is designed to be inherited, so you can create multiple node types, each with their own behavior, layout, and UI elements.  
  > When creating a subclass of `Node`, you will need to implement your own `Save` and `Load` functions for any additional properties or UI elements you add (see `ExampleNode` for a reference implementation).
- Edges are fully managed by the graph, In custom node types you will not need to manage edge creation or deletion.
- Context menus used to quickly create or modify nodes at any location in the graph.
- Designed for extensibility: you can override rendering, interaction, or serialization logic to fit your specific use case.
- When a Graph is Executed, Flow will start at the StartNode. When one node completes, it automatically triggers the next connected node, continuing the flow until it is stopped or an EndNode is reached.







---

## Usage

#### 1. Add a GraphView in XAML:
```
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>

    <!-- Toolbar with basic controls -->
    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="5">
        <Button Content="Save" Width="80" Margin="2" Click="SaveGraph_Click"/>
        <Button Content="Load" Width="80" Margin="2" Click="LoadGraph_Click"/>
        <Button Content="Clear" Width="80" Margin="2" Click="ClearGraph_Click"/>
        <Button Content="Recenter" Width="80" Margin="2" Click="Recenter_Click"/>
    </StackPanel>

    <!-- Graph canvas -->
    <xy:GraphView x:Name="graphView" Grid.Row="1" Background="#FFF8F8F8"/>
</Grid>
```

#### 2. Add your custom node to the graph with right click context menus:
```csharp
// Add ExampleNode to the graph's context menu
MenuItem exampleItem = new MenuItem { Header = "Create Example Node" };
exampleItem.Click += (s, e) => AddExampleNode();
graph.ContextMenu.Items.Add(exampleItem);
```
```csharp
private void AddExampleNode()
{
    ExampleNode node = new ExampleNode(graph);
    graphView.AddNode(node, graphView.rightClickPos.X - node.SpawnOffsetX, 
                             graphView.rightClickPos.Y - node.SpawnOffsetY);
}
```

#### 3. writing custom runtime logic:
```csharp
public override void Run()
{
    base.Run();
    // Custom runtime behavior here
}

public override void Completed()
{
    base.Completed();
    // Custom completion behavior here
}

public override void Error()
{
    // Custom error behavior here
    base.Error();
}
```

