"""Compare two Native AOT map files (IlcGenerateMapFile=true) and show where the size difference comes from.

Usage:
    python3 tools/mapdiff.py <bigger.map.xml> <smaller.map.xml>

Map files are written to <project>/obj/<config>/<tfm>/<rid>/native/<name>.map.xml.
"R" is the first (bigger) file, "G" the second.
"""
import re,sys,collections
def load(p):
    d={}
    for m in re.finditer(r'<(\w+) Name="([^"]*)" Length="(\d+)"',open(p,encoding='utf-8').read()):
        d[(m.group(1),m.group(2))]=int(m.group(3))
    return d
R=load(sys.argv[1]); G=load(sys.argv[2])
def bykind(d):
    c=collections.Counter()
    for (k,_),v in d.items(): c[k]+=v
    return c
kr,kg=bykind(R),bykind(G)
print("TOTAL map bytes  R=%d G=%d diff=%d"%(sum(R.values()),sum(G.values()),sum(R.values())-sum(G.values())))
print("\n== by node kind (diff > 2KB)")
for k in sorted(set(kr)|set(kg),key=lambda k:-(kr[k]-kg[k])):
    if abs(kr[k]-kg[k])>2048: print("%-28s R=%9d G=%9d diff=%+9d"%(k,kr[k],kg[k],kr[k]-kg[k]))
onlyR={k:v for k,v in R.items() if k not in G}; onlyG={k:v for k,v in G.items() if k not in R}
print("\nsymbols only in R: %d (%d bytes); only in G: %d (%d bytes)"%(len(onlyR),sum(onlyR.values()),len(onlyG),sum(onlyG.values())))
buckets=[('Reflection (System.Reflection / RuntimeType / Activator)',r'Reflection|RuntimeType|Activator|RuntimeMethod|RuntimeField|RuntimeProperty|CustomAttribute|MethodBase|MemberInfo|ParameterInfo|TypeInfo|Invoke|DynamicInvoke|Metadata'),
 ('Dictionary / Comparer / Hash', r'Dictionary|Comparer|HashHelpers|EqualityComparer|NonRandomized'),
 ('Text / Encoding / String', r'Encoding|Text_|String|Utf8|Ascii|Latin1'),
 ('Globalization', r'Globalization|Culture|Calendar'),
 ('Linq', r'Linq'),
 ('Async state machines', r'AsyncTaskMethodBuilder|AsyncStateMachine|d__'),
 ('Collections other', r'Collections'),
]
def classify(d):
    c=collections.Counter()
    for (k,n),v in d.items():
        for name,pat in buckets:
            if re.search(pat,n): c[name]+=v; break
        else: c['other:'+k]+=v
    return c
print("\n== only-in-Reflection symbols, classified by name")
for n,v in classify(onlyR).most_common(20): print("%-60s %9d"%(n,v))
print("\n== top 40 only-in-Reflection symbols")
for (k,n),v in sorted(onlyR.items(),key=lambda x:-x[1])[:40]: print("%7d %-20s %s"%(v,k,n[:110]))
