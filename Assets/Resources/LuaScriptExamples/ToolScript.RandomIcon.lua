Settings = {
    previewType="quad"
}

function onError(error)
    print ("http error: " .. error)
end

function onGetItem(result)
    svg = result
end

function onGetCollection(result, prefix)
    if result == nil then
        print ("Empty collection returned for: " .. prefix)
        return
    end
    local collection = json.parse(result)
    local categories = collection.categories
    local randomCategory
    if categories == nil then
        randomCategory = collection.uncategorized
    else
        randomCategory = randomItem(categories)
    end
    local randomItem = randomItem(randomCategory)
    local url = "https://api.iconify.design/".. prefix .. "/" .. randomItem .. ".svg"
        WebRequest:Get(url, onGetItem, onError)
end

function onGetAllCollections(result)
    local collections = json.parse(result)
    local randomCollection = randomKey(collections)
    local url = "https://api.iconify.design/collection?prefix=" .. randomCollection
    WebRequest:Get(url, onGetCollection , onError, {}, randomCollection)
end

function randomItem(tbl)
    return tbl[randomKey(tbl)]
end

function randomKey(tbl)
    local keyset = {}
    for k in pairs(tbl) do
        table.insert(keyset, k)
    end
    local key = keyset[math.random(#keyset)]
    return key
end

function requestNewIcon()
    WebRequest:Get("https://api.iconify.design/collections", onGetAllCollections)
end

function Start()
    requestNewIcon()
end

function OnTriggerReleased()
    requestNewIcon()
    if svg == nil then
        return {}
    else
        local strokes = Svg:ParseDocument(svg)
        strokes:Normalize(2) -- Scale and center inside a 2x2 square
        strokes:Resample(0.1) -- Evenly space all the points
        return strokes
    end
end
