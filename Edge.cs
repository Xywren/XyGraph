using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Collections;

namespace XyGraph
{
    public enum EdgeStyle { Linear, Bezier }

    public class Edge
    {

        private const double BEZIER_STRENGTH = 100;

        public Guid guid;

        public Port outputPort { get; private set; }
        public Port inputPort { get; private set; }
        public EdgeStyle style { get; private set; } = EdgeStyle.Bezier;
        public UIElement visual { get; private set; }
        private Graph graph;


        public Edge(Graph graph, Port fromPort, Port toPort)
        {
            if (fromPort == null || toPort == null) return;


            // port normalization 
            if (fromPort.direction == PortDirection.Output) this.outputPort = fromPort;
            else this.inputPort = fromPort;

            if (toPort.direction == PortDirection.Input) this.inputPort = toPort;
            else this.outputPort = toPort;

                guid = Guid.NewGuid();

            this.graph = graph;

            fromPort.ConnectionMade(this);
            toPort.ConnectionMade(this);


            // Allows nodes to reference each other when connections are made
            // [NodeInput] Node input;  --> input = <node connected to this port>
            // [NodeOutput] Node output; --> output = <node connected to this port>
            BindNodeReferenceToOwnerMember(fromPort, toPort.parentContainer?.node);
            BindNodeReferenceToOwnerMember (toPort, fromPort.parentContainer?.node);
        }

        public void ReDraw()
        {
            Point start = outputPort.socket.TranslatePoint(new Point(outputPort.socket.ActualWidth / 2, outputPort.socket.ActualHeight / 2), graph);
            Point end = inputPort.socket.TranslatePoint(new Point(inputPort.socket.ActualWidth / 2, inputPort.socket.ActualHeight / 2), graph);

            // determine stroke colour: prefer the input-port's colour when available
            Brush strokeBrush = null;
            if (inputPort != null && inputPort.direction == PortDirection.Input)
                strokeBrush = inputPort.colour;
            else if (outputPort != null && outputPort.direction == PortDirection.Input)
                strokeBrush = outputPort.colour;
            else
                strokeBrush = outputPort?.colour ?? Brushes.Black;

            if (style == EdgeStyle.Linear)
            {
                if (visual is Line line)
                {
                    line.X1 = start.X;
                    line.Y1 = start.Y;
                    line.X2 = end.X;
                    line.Y2 = end.Y;
                }
                else
                {
                    visual = new Line { Stroke = strokeBrush, StrokeThickness = 2, IsHitTestVisible = false, X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y };
                }
            }
            else // Bezier
            {
                Vector point1Offset = outputPort.direction == PortDirection.Output ? new Vector(BEZIER_STRENGTH, 0) : new Vector(-BEZIER_STRENGTH, 0);
                Vector point2Offset = inputPort.direction == PortDirection.Input ? new Vector(-BEZIER_STRENGTH, 0) : new Vector(BEZIER_STRENGTH, 0);

                PathGeometry geometry = new PathGeometry();
                PathFigure figure = new PathFigure { StartPoint = start };
                BezierSegment segment = new BezierSegment
                {
                    Point1 = start + point1Offset,
                    Point2 = end + point2Offset,
                    Point3 = end
                };
                figure.Segments.Add(segment);
                geometry.Figures.Add(figure);

                if (visual is Path path)
                {
                    path.Data = geometry;
                    path.Stroke = strokeBrush;
                }
                else
                {
                    visual = new Path { Stroke = strokeBrush, StrokeThickness = 2, IsHitTestVisible = false, Data = geometry };
                }
            }
        }

        public void Delete()
        {
            // unbind port-backed members before removing edge
            TryUnbindPortFromPeer(outputPort, inputPort.parentContainer?.node);
            TryUnbindPortFromPeer(inputPort, outputPort.parentContainer?.node);

            graph.Children.Remove(visual);
            graph.edges.Remove(this);
        }



        public JsonObject Save()
        {
            JsonObject obj = new JsonObject
            {
                ["id"] = guid.ToString(),
                ["from"] = outputPort?.guid.ToString() ?? string.Empty,
                ["to"] = inputPort?.guid.ToString() ?? string.Empty,
                ["style"] = style.ToString()
            };

            return obj;
        }

        // Make sure you load all nodes before attempting to load edges
        public static Edge Load(JsonObject obj, Graph graph)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            Guid id = Guid.Parse(obj["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString());
            Guid fromId = Guid.Parse(obj["from"]?.GetValue<string>() ?? Guid.Empty.ToString());
            Guid toId = Guid.Parse(obj["to"]?.GetValue<string>() ?? Guid.Empty.ToString());

            Port fromPort = graph.GetPortById(fromId);
            Port toPort = graph.GetPortById(toId);
            if (fromPort == null || toPort == null) throw new Exception("Port could not be found! maek sure you load all nodes before loading any edges");

            Edge e = graph.CreateEdge(fromPort, toPort);
            if (e == null) return null;

            e.guid = id;
            e.style = Enum.Parse<EdgeStyle>(obj["style"]?.GetValue<string>() ?? e.style.ToString());

            e.ReDraw();

            return e;
        }



        // Magic happens here
        private void BindNodeReferenceToOwnerMember(Port port, Node peer)
        {
            if (port == null || peer == null || port.ownerMember == null) return;

            // get the node that owns this port. if none exists, cannot bind
            Node ownerNode = port.parentContainer?.node;
            if (ownerNode == null) return;


            try
            {
                MemberInfo member = port.ownerMember;
                Type memberType = null;
                Func<object> getMemberValue = null;
                Action<object> setMemberValue = null;

                // get member data
                if (member is FieldInfo fieldInfo)
                {
                    memberType = fieldInfo.FieldType;
                    getMemberValue = () => fieldInfo.GetValue(ownerNode);
                    setMemberValue = (object v) => fieldInfo.SetValue(ownerNode, v);
                }
                else if (member is PropertyInfo propInfo)
                {
                    // if property is not writable we cannot bind
                    if (!propInfo.CanWrite) return;
                    memberType = propInfo.PropertyType;
                    getMemberValue = () => propInfo.GetValue(ownerNode);
                    setMemberValue = (object v) => propInfo.SetValue(ownerNode, v);
                }
                if (memberType == null || getMemberValue == null || setMemberValue == null) return;

                // If the member is a single Node reference (e.g. `[NodeInput] public Node input;`),
                // then set input = <the node that was just connected to this port>.
                if (typeof(Node).IsAssignableFrom(memberType))
                {
                    setMemberValue(peer);
                }

                // If the member is a List<T>  (e.g. `[NodeMultiOutput] public Node input;`),
                // then set input[i] = <the node that was just connected to this port>.
                else if (typeof(IList).IsAssignableFrom(memberType))
                {
                    // parse the class member as a List<T>
                    object listObj = getMemberValue();
                    IList list = listObj as IList;

                    if (list != null)
                    {
                        // if port specifies an index, add the peer at that index
                        if (port.ownerIndex >= 0)
                        {
                            int idx = port.ownerIndex;
                            while (list.Count <= idx) list.Add(null);
                            list[idx] = peer;
                        }
                        // otherwise append to the end of the list
                        else
                        {
                            if (!list.Contains(peer)) list.Add(peer);
                        }
                    }
                    else
                    {
                        // No list instance exists yet on the owner. Create a concrete
                        // List<T> using the member's generic argument, populate it and
                        // set it back onto the owner so future bindings can use it.
                        if (memberType.IsGenericType)
                        {
                            // figure out the element type of the list (T in List<T>)
                            Type elemType = memberType.GetGenericArguments()[0];
                            Type listType = typeof(List<>).MakeGenericType(new Type[] { elemType });

                            //instantiate the list and populate it
                            object newListObj = Activator.CreateInstance(listType);
                            IList newList = newListObj as IList;
                            if (newList != null)
                            {
                                // if port specifies an index, add the peer at that index
                                if (port.ownerIndex >= 0)
                                {
                                    int idx = port.ownerIndex;
                                    while (newList.Count <= idx) newList.Add(null);
                                    newList[idx] = peer;
                                }
                                // otherwise append to the end of the list
                                else
                                {
                                    newList.Add(peer);
                                }
                                // set the new list back onto the owner member
                                setMemberValue(newList);
                            }
                        }
                    }
                }
            }
            catch
            {
                // this should probably throw some kind of error
            }
        }
        private void TryUnbindPortFromPeer(Port port, Node peer)
        {
            if (port == null || peer == null || port.ownerMember == null) return;

            Node ownerNode = port.parentContainer?.node;
            if (ownerNode == null) return;

            try
            {
                MemberInfo member = port.ownerMember;
                Type memberType = null;
                Func<object> getter = null;
                Action<object> setter = null;

                // get member data
                if (member is FieldInfo fi)
                {
                    memberType = fi.FieldType;
                    getter = () => fi.GetValue(ownerNode);
                    setter = (object v) => fi.SetValue(ownerNode, v);
                }
                else if (member is PropertyInfo pi)
                {
                    if (!pi.CanWrite) return;
                    memberType = pi.PropertyType;
                    getter = () => pi.GetValue(ownerNode);
                    setter = (object v) => pi.SetValue(ownerNode, v);
                }
                if (memberType == null || getter == null) return;

                // single Node reference: if currently references peer, clear it
                if (typeof(Node).IsAssignableFrom(memberType))
                {
                    object cur = getter();
                    if (object.ReferenceEquals(cur, peer)) setter?.Invoke(null);
                }
                // list-backed multi-output: clear the specific slot or remove the peer
                else if (typeof(IList).IsAssignableFrom(memberType))
                {
                    // if member is a List<T>
                    object listObj = getter();
                    IList list = listObj as IList;
                    if (list != null)
                    {
                        // if port specifies an index, set that index to null
                        if (port.ownerIndex >= 0)
                        {
                            int idx = port.ownerIndex;
                            if (idx < list.Count && object.ReferenceEquals(list[idx], peer))
                            {
                                list[idx] = null;
                            }
                        }
                        // otherwise remove the peer from the list
                        else
                        {
                            if (list.Contains(peer)) list.Remove(peer);
                        }
                    }
                }
            }
            catch
            {
                // ignore unbind errors
            }
        }
    }
}