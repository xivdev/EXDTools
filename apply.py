import sys

from pathlib import Path
from ruamel.yaml import YAML
from ruamel.yaml.comments import CommentedMap
from ruamel.yaml.comments import CommentedSeq
from ruamel.yaml.compat import StringIO

APPLY_PENDING = sys.argv[1] == 'true'

class MyYAML(YAML):
    def dump(self, data, stream=None, **kw):
        inefficient = False
        if stream is None:
            inefficient = True
            stream = StringIO()
        YAML.dump(self, data, stream, **kw)
        if inefficient:
            return stream.getvalue()

def order_keys(data, order):
    if not isinstance(data, CommentedMap):
        raise Exception("Not a CommentedMap")

    reordered = CommentedMap()
    for key in order:
        if key in data:
            comment = data.ca.items.get(key, None)
            reordered[key] = data.pop(key)
            if comment:
                reordered.ca.items[key] = comment

    for key in list(data):
        comment = data.ca.items.get(key, None)
        reordered[key] = data.pop(key)
        if comment:
            reordered.ca.items[key] = comment

    data.clear()
    data.update(reordered)
    return data

def recurse(data, *args):
    for a in args:
        data = a(data)

    ret = []
    for fld in data.get('fields', []):
        ret.append(recurse(fld, *args))

    if len(ret) != 0:
        comment = data.ca.items.get('fields', None)
        data['fields'] = ret
        if comment:
            data.ca.items['fields'] = comment
    return data

def apply_flow_style(data):
    if 'targets' in data:
        comment = data.ca.items.get('targets', None)
        seq = CommentedSeq(data['targets'])
        seq.fa.set_flow_style()
        data['targets'] = seq
        if comment:
            data.ca.items['targets'] = comment
    return data

def apply_names(data):
    if APPLY_PENDING:
        if 'pendingName' in data:
            comment = data.ca.items.get('pendingName', None)
            data['name'] = data.pop('pendingName')
            if comment:
                data.ca.items['name'] = comment
    return data

def apply_changes(data):
    if APPLY_PENDING:
        if 'pendingFields' in data:
            comment = data.ca.items.get('pendingFields', None)
            data['fields'] = data.pop('pendingFields')
            if comment:
                data.ca.items['fields'] = comment
    return data

yaml = MyYAML()
yaml.preserve_quotes = True
yaml.width = 4096
yaml.indent(sequence=4, offset=2)

for path in sorted(list(Path('./schemas').glob('*/*.yml')), key = lambda p: p.stem):
    inp = path.read_text().strip()
    data = yaml.load(path)

    data = order_keys(data, ['name', 'displayField', 'fields', 'pendingFields', 'relations'])
    data = apply_changes(data)

    ret = []
    for fld in data['fields']:
        ret.append(recurse(fld, lambda f: order_keys(f, ['name', 'pendingName', 'comment', 'type', 'count', 'fields']), apply_flow_style, apply_names))
    if len(ret) != 0:
        comment = data.ca.items.get('fields', None)
        data['fields'] = ret
        if comment:
            data.ca.items['fields'] = comment

    if 'pendingFields' in data:
        ret = []
        for fld in data['pendingFields']:
            ret.append(recurse(fld, lambda f: order_keys(f, ['name', 'pendingName', 'type', 'count', 'fields']), apply_flow_style, apply_names))
        if len(ret) != 0:
            comment = data.ca.items.get('pendingFields', None)
            data['pendingFields'] = ret
            if comment:
                data.ca.items['pendingFields'] = comment

    output = yaml.dump(data).strip()
    if inp != output:
        print(path, 'changed')
        #print(output)
        path.write_text(output)
